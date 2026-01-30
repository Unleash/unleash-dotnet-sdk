using System.Net;

namespace Unleash.Utilities
{
    public class FetchingToggleBootstrapUrlFailedException : UnleashException
    {
        public FetchingToggleBootstrapUrlFailedException(string errorMessage, HttpStatusCode statusCode) : base(errorMessage)
        {
            StatusCode = statusCode;
        }

        public FetchingToggleBootstrapUrlFailedException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; set; }
    }
}
