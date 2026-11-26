using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace MU_Marketplace.Infrastructure
{
    public class StripeLineItem
    {
        public string Name { get; set; }
        public long UnitAmountPaise { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class StripeSessionResult
    {
        public bool Success { get; set; }
        public string SessionId { get; set; }
        public string CheckoutUrl { get; set; }
        public bool IsPaid { get; set; }
        public string PaymentIntentId { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public string Error { get; set; }
    }

    public class StripeRefundResult
    {
        public bool Success { get; set; }
        public string RefundId { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Lightweight Stripe REST API client using built-in HttpWebRequest and JavaScriptSerializer
    /// so no external NuGet packages are required.
    /// </summary>
    public static class StripeHelper
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private static string SecretKey => ConfigurationManager.AppSettings["Stripe:SecretKey"];

        public static bool IsConfigured()
        {
            string key = SecretKey;
            return !string.IsNullOrWhiteSpace(key)
                && !key.Contains("YOUR_KEY_HERE")
                && (key.StartsWith("sk_test_") || key.StartsWith("sk_live_"));
        }

        public static StripeSessionResult CreateCheckoutSession(
            IEnumerable<StripeLineItem> items,
            string successUrl,
            string cancelUrl,
            Dictionary<string, string> metadata)
        {
            if (!IsConfigured())
            {
                return new StripeSessionResult { Success = false, Error = "Stripe SecretKey is not configured in Web.config." };
            }

            var form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("mode", "payment"),
                new KeyValuePair<string, string>("success_url", successUrl),
                new KeyValuePair<string, string>("cancel_url", cancelUrl)
            };

            int idx = 0;
            foreach (var item in items)
            {
                form.Add(new KeyValuePair<string, string>($"line_items[{idx}][price_data][currency]", "inr"));
                form.Add(new KeyValuePair<string, string>($"line_items[{idx}][price_data][unit_amount]", item.UnitAmountPaise.ToString()));
                form.Add(new KeyValuePair<string, string>($"line_items[{idx}][price_data][product_data][name]", item.Name));
                form.Add(new KeyValuePair<string, string>($"line_items[{idx}][quantity]", item.Quantity.ToString()));
                idx++;
            }

            if (metadata != null)
            {
                foreach (var kv in metadata)
                {
                    form.Add(new KeyValuePair<string, string>($"metadata[{kv.Key}]", kv.Value ?? ""));
                }
            }

            string raw = SendForm("POST", "https://api.stripe.com/v1/checkout/sessions", form, out string httpError);
            if (raw == null)
            {
                return new StripeSessionResult { Success = false, Error = httpError };
            }

            var dict = Json.Deserialize<Dictionary<string, object>>(raw);
            return new StripeSessionResult
            {
                Success = true,
                SessionId = dict.ContainsKey("id") ? Convert.ToString(dict["id"]) : null,
                CheckoutUrl = dict.ContainsKey("url") ? Convert.ToString(dict["url"]) : null
            };
        }

        public static StripeSessionResult GetCheckoutSession(string sessionId)
        {
            if (!IsConfigured())
            {
                return new StripeSessionResult { Success = false, Error = "Stripe SecretKey is not configured in Web.config." };
            }

            string url = "https://api.stripe.com/v1/checkout/sessions/" + HttpUtility.UrlEncode(sessionId);
            string raw = SendForm("GET", url, null, out string httpError);
            if (raw == null)
            {
                return new StripeSessionResult { Success = false, Error = httpError };
            }

            var dict = Json.Deserialize<Dictionary<string, object>>(raw);
            string paymentStatus = dict.ContainsKey("payment_status") ? Convert.ToString(dict["payment_status"]) : "";
            string paymentIntent = dict.ContainsKey("payment_intent") ? Convert.ToString(dict["payment_intent"]) : null;

            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (dict.ContainsKey("metadata") && dict["metadata"] is Dictionary<string, object> rawMeta)
            {
                foreach (var kv in rawMeta)
                {
                    meta[kv.Key] = Convert.ToString(kv.Value);
                }
            }

            return new StripeSessionResult
            {
                Success = true,
                SessionId = sessionId,
                IsPaid = string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase),
                PaymentIntentId = paymentIntent,
                Metadata = meta
            };
        }

        public static StripeRefundResult CreateRefund(string paymentIntentId)
        {
            if (!IsConfigured())
            {
                return new StripeRefundResult { Success = false, Error = "Stripe SecretKey is not configured in Web.config." };
            }
            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                return new StripeRefundResult { Success = false, Error = "No Stripe PaymentIntent ID recorded for this order." };
            }

            var form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("payment_intent", paymentIntentId)
            };

            string raw = SendForm("POST", "https://api.stripe.com/v1/refunds", form, out string httpError);
            if (raw == null)
            {
                return new StripeRefundResult { Success = false, Error = httpError };
            }

            var dict = Json.Deserialize<Dictionary<string, object>>(raw);
            return new StripeRefundResult
            {
                Success = true,
                RefundId = dict.ContainsKey("id") ? Convert.ToString(dict["id"]) : null
            };
        }

        private static string SendForm(string method, string url, List<KeyValuePair<string, string>> formFields, out string error)
        {
            error = null;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Headers["Authorization"] = "Bearer " + SecretKey;

            if (method == "POST" && formFields != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < formFields.Count; i++)
                {
                    if (i > 0) sb.Append('&');
                    sb.Append(HttpUtility.UrlEncode(formFields[i].Key));
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(formFields[i].Value ?? ""));
                }

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = bytes.Length;
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string body = null;
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        body = reader.ReadToEnd();
                    }
                }
                error = ExtractStripeError(body) ?? ex.Message;
                return null;
            }
        }

        private static string ExtractStripeError(string jsonBody)
        {
            if (string.IsNullOrWhiteSpace(jsonBody)) return null;
            try
            {
                var dict = Json.Deserialize<Dictionary<string, object>>(jsonBody);
                if (dict != null && dict.ContainsKey("error") && dict["error"] is Dictionary<string, object> errObj)
                {
                    if (errObj.ContainsKey("message"))
                        return Convert.ToString(errObj["message"]);
                }
            }
            catch { }
            return null;
        }
    }
}
