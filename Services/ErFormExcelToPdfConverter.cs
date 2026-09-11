using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ERPaperless.Services
{
    /// <summary>
    /// Converts in-memory SpreadsheetML (.xls) ER Form bytes to a compact PDF,
    /// preserving cell text, merges, and approximate style colors.
    /// </summary>
    public static class ErFormExcelToPdfConverter
    {
        private const int ContentFirstCol = 2;
        private const int ContentLastCol = 11;
        private const int ColCount = 10;

        public static byte[] ConvertToPdf(byte[] excelBytes)
        {
            if (excelBytes == null || excelBytes.Length == 0)
                return null;

            var xml = Encoding.UTF8.GetString(StripBom(excelBytes));
            var doc = XDocument.Parse(xml);
            XNamespace ss = "urn:schemas-microsoft-com:office:spreadsheet";

            var styleMap = LoadStyles(doc.Root, ss);
            var table = doc.Root?
                .Element(ss + "Worksheet")?
                .Element(ss + "Table");
            if (table == null)
                return null;

            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 28f, 28f, 28f, 28f);
                var writer = PdfWriter.GetInstance(document, ms);
                writer.SetFullCompression();
                writer.CompressionLevel = PdfStream.BEST_COMPRESSION;
                document.Open();

                PdfPTable pdfTable = null;
                float[] colWidths = BuildColWidths();

                foreach (var rowEl in table.Elements(ss + "Row"))
                {
                    if (pdfTable == null)
                    {
                        pdfTable = new PdfPTable(ColCount)
                        {
                            WidthPercentage = 100f,
                            SpacingBefore = 0f,
                            SpacingAfter = 0f
                        };
                        pdfTable.SetWidths(colWidths);
                    }

                    var cellsByIndex = new Dictionary<int, XElement>();
                    foreach (var cellEl in rowEl.Elements(ss + "Cell"))
                    {
                        var indexAttr = cellEl.Attribute(ss + "Index")?.Value;
                        int index;
                        if (!int.TryParse(indexAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                            continue;
                        cellsByIndex[index] = cellEl;
                    }

                    double rowHeight = 15;
                    double.TryParse(rowEl.Attribute(ss + "Height")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out rowHeight);
                    var minHeight = Math.Max(10f, (float)rowHeight * 0.75f);

                    var col = ContentFirstCol;
                    while (col <= ContentLastCol)
                    {
                        XElement cellEl;
                        cellsByIndex.TryGetValue(col, out cellEl);

                        var mergeAcross = 0;
                        if (cellEl != null)
                        {
                            int.TryParse(cellEl.Attribute(ss + "MergeAcross")?.Value,
                                NumberStyles.Integer, CultureInfo.InvariantCulture, out mergeAcross);
                        }

                        var span = Math.Min(1 + mergeAcross, ContentLastCol - col + 1);
                        var text = cellEl?.Element(ss + "Data")?.Value ?? string.Empty;
                        var styleId = cellEl?.Attribute(ss + "StyleID")?.Value ?? "Default";
                        CellStyle style;
                        if (!styleMap.TryGetValue(styleId, out style))
                            style = CellStyle.Default;

                        var phrase = new Phrase(text ?? string.Empty, style.Font);
                        var pdfCell = new PdfPCell(phrase)
                        {
                            Colspan = span,
                            BorderColor = new BaseColor(229, 231, 235),
                            BorderWidth = 0.4f,
                            Padding = 3f,
                            MinimumHeight = minHeight,
                            BackgroundColor = style.Background,
                            HorizontalAlignment = style.HAlign,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            NoWrap = false
                        };

                        pdfTable.AddCell(pdfCell);
                        col += span;
                    }
                }

                if (pdfTable != null)
                    document.Add(pdfTable);

                document.Close();
                return CompressPdf(ms.ToArray());
            }
        }

        /// <summary>
        /// Re-writes the PDF with full object streams / best compression to shrink size.
        /// </summary>
        public static byte[] CompressPdf(byte[] pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0)
                return pdfBytes;

            try
            {
                using (var input = new MemoryStream(pdfBytes))
                using (var reader = new PdfReader(input))
                using (var output = new MemoryStream())
                {
                    using (var stamper = new PdfStamper(reader, output))
                    {
                        stamper.Writer.SetFullCompression();
                        stamper.Writer.CompressionLevel = PdfStream.BEST_COMPRESSION;
                        stamper.SetFullCompression();
                        stamper.FormFlattening = true;
                    }

                    var compressed = output.ToArray();
                    return compressed.Length > 0 && compressed.Length < pdfBytes.Length
                        ? compressed
                        : pdfBytes;
                }
            }
            catch
            {
                return pdfBytes;
            }
        }

        private static float[] BuildColWidths()
        {
            // Equal share for the 10 content columns used by the Excel layout.
            return Enumerable.Repeat(1f, ColCount).ToArray();
        }

        private static byte[] StripBom(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                var trimmed = new byte[bytes.Length - 3];
                Buffer.BlockCopy(bytes, 3, trimmed, 0, trimmed.Length);
                return trimmed;
            }
            return bytes;
        }

        private static Dictionary<string, CellStyle> LoadStyles(XElement root, XNamespace ss)
        {
            var map = new Dictionary<string, CellStyle>(StringComparer.OrdinalIgnoreCase)
            {
                ["Default"] = CellStyle.Default
            };

            var styles = root?.Element(ss + "Styles");
            if (styles == null)
                return map;

            foreach (var styleEl in styles.Elements(ss + "Style"))
            {
                var id = styleEl.Attribute(ss + "ID")?.Value;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var fontEl = styleEl.Element(ss + "Font");
                var interiorEl = styleEl.Element(ss + "Interior");
                var alignEl = styleEl.Element(ss + "Alignment");

                var size = 9f;
                float.TryParse(fontEl?.Attribute(ss + "Size")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out size);
                if (size < 7f) size = 7f;
                if (size > 14f) size = 14f;

                var bold = string.Equals(fontEl?.Attribute(ss + "Bold")?.Value, "1", StringComparison.Ordinal);
                var fontColor = ParseColor(fontEl?.Attribute(ss + "Color")?.Value, new BaseColor(31, 42, 55));
                var fill = ParseColor(interiorEl?.Attribute(ss + "Color")?.Value, BaseColor.WHITE);

                var hAlignRaw = alignEl?.Attribute(ss + "Horizontal")?.Value ?? "";
                int hAlign = Element.ALIGN_LEFT;
                if (string.Equals(hAlignRaw, "Center", StringComparison.OrdinalIgnoreCase))
                    hAlign = Element.ALIGN_CENTER;
                else if (string.Equals(hAlignRaw, "Right", StringComparison.OrdinalIgnoreCase))
                    hAlign = Element.ALIGN_RIGHT;

                var font = FontFactory.GetFont(
                    FontFactory.HELVETICA,
                    size * 0.85f,
                    bold ? Font.BOLD : Font.NORMAL,
                    fontColor);

                map[id] = new CellStyle
                {
                    Font = font,
                    Background = fill,
                    HAlign = hAlign
                };
            }

            return map;
        }

        private static BaseColor ParseColor(string hex, BaseColor fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            hex = hex.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal))
                hex = hex.Substring(1);
            if (hex.Length != 6)
                return fallback;

            int r, g, b;
            if (!int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
                || !int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
                || !int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            {
                return fallback;
            }

            return new BaseColor(r, g, b);
        }

        private sealed class CellStyle
        {
            public static readonly CellStyle Default = new CellStyle
            {
                Font = FontFactory.GetFont(FontFactory.HELVETICA, 8f, Font.NORMAL, new BaseColor(31, 42, 55)),
                Background = BaseColor.WHITE,
                HAlign = Element.ALIGN_LEFT
            };

            public Font Font { get; set; }
            public BaseColor Background { get; set; }
            public int HAlign { get; set; }
        }
    }
}
