namespace ERPaperless.Application.Common
{
    public class OperationResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }

        public static OperationResult Success(string message = null)
        {
            return new OperationResult { Ok = true, Message = message };
        }

        public static OperationResult Fail(string message)
        {
            return new OperationResult { Ok = false, Message = message };
        }
    }

    public class OperationResult<T> : OperationResult
    {
        public T Data { get; set; }

        public static OperationResult<T> Success(T data, string message = null)
        {
            return new OperationResult<T> { Ok = true, Message = message, Data = data };
        }

        public static OperationResult<T> Fail(string message)
        {
            return new OperationResult<T> { Ok = false, Message = message };
        }
    }
}
