using System;

namespace Курсовая_ооп.Core
{
    public class StudioException : Exception
    {
        public DateTime ErrorTimestamp { get; }
        public string ErrorCode { get; }

        public StudioException(string message, string errorCode) : base(message)
        {
            ErrorTimestamp = DateTime.Now;
            ErrorCode = errorCode;
        }
    }
}