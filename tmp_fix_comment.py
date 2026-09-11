from pathlib import Path
p = Path(r'd:\AMC PROJECTS\ER Module\ERPaperless\Services\ErFormExcelExporter.cs')
text = p.read_text(encoding='utf-8')
text = text.replace(
    '/// Visual ER Form Excel export Ã¢â‚¬â€ card layout matching the on-screen form,\n'
    '    /// fixed compact width for 11-inch tablet viewing (no wide spreadsheet report layout).',
    '/// Visual ER Form Excel export - card layout matching the on-screen form,\n'
    '    /// fixed compact width for 11-inch tablet viewing (Excel 2003 SpreadsheetML .xls).'
)
# also handle if already partially fixed
import re
text = re.sub(
    r'/// Visual ER Form Excel export .*?\n\s*/// fixed compact width.*?\n',
    '/// Visual ER Form Excel export - card layout matching the on-screen form,\n'
    '    /// fixed compact width for 11-inch tablet viewing (Excel 2003 SpreadsheetML .xls).\n',
    text,
    count=1
)
p.write_text(text, encoding='utf-8')
print('ok')
