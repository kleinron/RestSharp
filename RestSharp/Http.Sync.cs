﻿#region License
//   Copyright 2010 John Sheehan
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License. 
#endregion

#if FRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

#if !MONOTOUCH
using System.Web;
#endif

using RestSharp.Extensions;

namespace RestSharp
{
	/// <summary>
	/// HttpWebRequest wrapper (sync methods)
	/// </summary>
	public partial class Http
	{
		/// <summary>
		/// Execute a POST request
		/// </summary>
		public HttpResponse Post()
		{
			return PostPutInternal("POST");
		}

		/// <summary>
		/// Execute a PUT request
		/// </summary>
		public HttpResponse Put()
		{
			return PostPutInternal("PUT");
		}

		/// <summary>
		/// Execute a GET request
		/// </summary>
		public HttpResponse Get()
		{
			return GetStyleMethodInternal("GET");
		}

		/// <summary>
		/// Execute a HEAD request
		/// </summary>
		public HttpResponse Head()
		{
			return GetStyleMethodInternal("HEAD");
		}

		/// <summary>
		/// Execute an OPTIONS request
		/// </summary>
		public HttpResponse Options()
		{
			return GetStyleMethodInternal("OPTIONS");
		}

		/// <summary>
		/// Execute a DELETE request
		/// </summary>
		public HttpResponse Delete()
		{
			return GetStyleMethodInternal("DELETE");
		}

		private HttpResponse GetStyleMethodInternal(string method)
		{
			var webRequest = ConfigureWebRequest(method, Url);

			return GetResponse(webRequest);
		}

		private HttpResponse PostPutInternal(string method)
		{
			var webRequest = ConfigureWebRequest(method, Url);

			PreparePostData(webRequest);

			WriteRequestBody(webRequest);
			return GetResponse(webRequest);
		}

		partial void AddSyncHeaderActions()
		{
			_restrictedHeaderActions.Add("Connection", (r, v) => r.Connection = v);
			_restrictedHeaderActions.Add("Content-Length", (r, v) => r.ContentLength = Convert.ToInt64(v));
			_restrictedHeaderActions.Add("Expect", (r, v) => r.Expect = v);
			_restrictedHeaderActions.Add("If-Modified-Since", (r, v) => r.IfModifiedSince = Convert.ToDateTime(v));
			_restrictedHeaderActions.Add("Referer", (r, v) => r.Referer = v);
			_restrictedHeaderActions.Add("Transfer-Encoding", (r, v) => { r.TransferEncoding = v; r.SendChunked = true; });
			_restrictedHeaderActions.Add("User-Agent", (r, v) => r.UserAgent = v);
		}

		private HttpResponse GetResponse(HttpWebRequest request)
		{
			var response = new HttpResponse();
			response.ResponseStatus = ResponseStatus.None;

			try
			{
				var webResponse = GetRawResponse(request);
				ExtractResponseData(response, webResponse);
			}
			catch (Exception ex)
			{
				response.ErrorMessage = ex.Message;
				response.ErrorException = ex;
				response.ResponseStatus = ResponseStatus.Error;
			}

			return response;
		}

		private HttpWebResponse GetRawResponse(HttpWebRequest request)
		{
			try
			{
				return (HttpWebResponse)request.GetResponse();
			}
			catch (WebException ex)
			{
				if (ex.Response is HttpWebResponse)
				{
					return ex.Response as HttpWebResponse;
				}
				throw;
			}
		}

		private void PreparePostData(HttpWebRequest webRequest)
		{
			if (HasFiles)
			{
				webRequest.ContentType = GetMultipartFormContentType();
				WriteMultipartFormData(webRequest);
			}

			PreparePostBody(webRequest);
		}

		private void WriteMultipartFormData(HttpWebRequest webRequest)
		{
			var encoding = Encoding.UTF8;
			using (Stream formDataStream = webRequest.GetRequestStream())
			{
				foreach (var param in Parameters)
				{
					var postData = GetMultipartFormData (param);
					var postDataBytes = encoding.GetBytes(postData);
					formDataStream.Write(postDataBytes, 0, postDataBytes.Length);
				}

				foreach (var file in Files)
				{
				    // Add just the first part of this param, since we will write the file data directly to the Stream
				    string header = GetMultipartFileHeader(file);
				    var headerBytes = encoding.GetBytes(header);

				    formDataStream.Write(headerBytes, 0, headerBytes.Length);
				    // Write the file data directly to the Stream, rather than serializing it to a string.
				    file.Writer(formDataStream);
				    string lineEnding = Environment.NewLine;
				    formDataStream.Write(encoding.GetBytes(lineEnding), 0, lineEnding.Length);
				}

				string footer = GetMultipartFooter();
				var footerBytes = encoding.GetBytes(footer);
				formDataStream.Write(footerBytes, 0, footerBytes.Length);
			}
		}

		private void WriteRequestBody(HttpWebRequest webRequest)
		{
			if (HasBody)
			{
				var encoding = Encoding.UTF8;
				var bytes = encoding.GetBytes(RequestBody);

				webRequest.ContentLength = bytes.Length;

				using (var requestStream = webRequest.GetRequestStream())
				{
					requestStream.Write(bytes, 0, bytes.Length);
				}
			}
		}

		private HttpWebRequest ConfigureWebRequest(string method, Uri url)
		{
			var webRequest = (HttpWebRequest)WebRequest.Create(url);
			webRequest.UseDefaultCredentials = false;
			ServicePointManager.Expect100Continue = false;

			AppendHeaders(webRequest);
			AppendCookies(webRequest);

			webRequest.Method = method;

			// make sure Content-Length header is always sent since default is -1
			if (!HasFiles)
			{
				webRequest.ContentLength = 0;
			}

			webRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.None;

			if (UserAgent.HasValue())
			{
				webRequest.UserAgent = UserAgent;
			}

			if (Timeout != 0)
			{
				webRequest.Timeout = Timeout;
			}

			if (Credentials != null)
			{
				webRequest.Credentials = Credentials;
			}

			if (Proxy != null)
			{
				webRequest.Proxy = Proxy;
			}

			webRequest.AllowAutoRedirect = FollowRedirects;
			if (FollowRedirects && MaxRedirects.HasValue)
			{
				webRequest.MaximumAutomaticRedirections = MaxRedirects.Value; 
			}

			return webRequest;
		}
	}
}
#endif