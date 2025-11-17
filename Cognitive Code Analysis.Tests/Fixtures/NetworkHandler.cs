namespace CognitiveCodeAnalysis.Tests.Fixtures;

public class NetworkHandler
{
    private bool isConnected = false;
    private int timeout = 30;
    private string lastError = "";

    public bool ProcessNetworkRequest(
        string url,
        string method,
        Dictionary<string, string> headers,
        string body,
        int retryCount,
        bool validateSSL,
        bool followRedirects)
    {
        bool success = false;
        int attempts = 0;
        int statusCode = 0;

        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (!validateSSL)
        {
            if (url.StartsWith("https://"))
            {
                return false;
            }
        }

        while (attempts < retryCount)
        {
            try
            {
                if (method == "GET")
                {
                    if (headers != null && headers.Count > 0)
                    {
                        statusCode = 200;
                    }
                    else
                    {
                        statusCode = 200;
                    }
                }
                else if (method == "POST")
                {
                    if (string.IsNullOrEmpty(body))
                    {
                        return false;
                    }
                    else
                    {
                        if (headers != null && headers.ContainsKey("Content-Type"))
                        {
                            statusCode = 201;
                        }
                        else
                        {
                            statusCode = 201;
                        }
                    }
                }
                else if (method == "PUT")
                {
                    statusCode = 200;
                }
                else if (method == "DELETE")
                {
                    statusCode = 204;
                }
                else
                {
                    return false;
                }

                if (statusCode >= 200 && statusCode < 300)
                {
                    success = true;
                    break;
                }
                else if (statusCode >= 300 && statusCode < 400)
                {
                    if (followRedirects)
                    {
                        attempts++;
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    attempts++;
                    continue;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                attempts++;

                if (attempts >= retryCount)
                {
                    return false;
                }
            }
        }

        if (success)
        {
            isConnected = true;
            return true;
        }
        else
        {
            isConnected = false;
            return false;
        }
    }

    public string HandleResponse(
        int statusCode,
        string responseBody,
        Dictionary<string, string> responseHeaders,
        bool parseJson,
        bool validateSchema,
        bool logResponse)
    {
        string result = "";
        bool isValid = false;

        if (statusCode < 200 || statusCode >= 300)
        {
            return "Error: Invalid status code";
        }

        if (string.IsNullOrEmpty(responseBody))
        {
            return "Error: Empty response body";
        }

        if (parseJson)
        {
            if (responseBody.StartsWith("{") || responseBody.StartsWith("["))
            {
                if (validateSchema)
                {
                    if (responseBody.Contains("\"id\""))
                    {
                        isValid = true;
                    }
                    else
                    {
                        return "Error: Invalid JSON schema";
                    }
                }
                else
                {
                    isValid = true;
                }
            }
            else
            {
                return "Error: Response is not valid JSON";
            }
        }
        else
        {
            isValid = true;
        }

        if (isValid)
        {
            result = responseBody;

            if (logResponse)
            {
                result = "LOGGED: " + result;
            }

            if (responseHeaders != null && responseHeaders.ContainsKey("Content-Type"))
            {
                result = result + " [Content-Type: " + responseHeaders["Content-Type"] + "]";
            }
        }

        return result;
    }
}

