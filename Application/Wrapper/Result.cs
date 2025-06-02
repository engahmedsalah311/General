using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Wrapper
{
    public class Result<T>
    {
        public T? Data { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public static Result<T> SuccessResult(T data, string message = "Success")
        => new Result<T> { Success = true, Data = data, Message = message };

        public static Result<T> FailureResult(string message)
            => new Result<T> { Success = false, Message = message };
    }
   
}
