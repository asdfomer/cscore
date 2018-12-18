﻿using com.csutil;
using com.csutil.http.cookies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace com.csutil {

    public static class UnityWebRequestCookieExtensions {

        /// <summary> set all current cookies for each new request </summary>
        public static bool ApplyAllCookiesToRequest(this UnityWebRequest self) {
            if (self.url.StartsWith("file://")) {
                Log.d("Will not apply cookies for request to file " + self.url);
                return false;
            }
            return self.SetCookies(LoadStoredCookiesForUri(new Uri(self.url)));
        }

        private static List<Cookie> LoadStoredCookiesForUri(Uri uri) {
            CookieJar ccc = IoC.inject.Get<CookieJar>(uri);
            var c = ccc.GetCookies(new CookieAccessInfo(uri.Host, uri.AbsolutePath));
            if (c.IsNullOrEmpty() && uri.Scheme.Equals("https")) { c = ccc.GetCookies(new CookieAccessInfo(uri.Host, uri.AbsolutePath, true)); }
            return c;
        }

        /// <summary> can be used to manually set a specific set of cookies, normally not needed since cookies are applied automatically by applyAllCookies()! </summary>
        private static bool SetCookies(this UnityWebRequest self, List<Cookie> cookieList) {
            if (cookieList.IsNullOrEmpty()) {
                Log.d("   > This request will have no cookies included (cookie string in cookie jar is empty), url=" + self.url);
                return false;
            }
            bool allCookiesSetCorrectly = true;
            string cookieString = "";
            foreach (var cookie in cookieList) {
                try {
                    var newcookies = cookieString + cookie.name + "=" + cookie.value + ";";
                    // https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.SetRequestHeader.html :
                    self.SetRequestHeader("Cookie", newcookies);
                    cookieString = newcookies;
                }
                catch (Exception e) {
                    Log.e("Name or value invalid: " + cookie.name + "=" + cookie.value + "    - full cookie string=" + cookieString, e);
                    allCookiesSetCorrectly = false;
                }
            }
            var allUsedCookiesForRequest = self.GetRequestHeader("Cookie");
            Log.d("  > applyAllCookies will add this Cookie header: " + allUsedCookiesForRequest);
            if (allCookiesSetCorrectly) { // cookies were set so check if they are in there:
                try {
                    var wwwCookies = self.GetRequestHeader("Cookie");
                    if (wwwCookies.IsNullOrEmpty()) { throw new Exception("No 'Cookie' Header included in the request!"); }
                }
                catch (Exception e) { Log.e(e); }
            }
            return allCookiesSetCorrectly;
        }

        public static bool SaveAllNewCookiesFromResponse(this UnityWebRequest self) {
            try {
                List<string> cookies = GetResponseHeaders(self, "Set-Cookie");
                if (cookies.IsNullOrEmpty()) { return false; }
                foreach (var cookieString in cookies) { AddCookie(self, cookieString); }
                return true;
            }
            catch (Exception e) { Log.e(e); return false; }
        }

        private static List<string> GetResponseHeaders(UnityWebRequest self, string headerName) {
            List<string> cookies = new List<string>();
            cookies.Add(self.GetResponseHeader(headerName));
            return cookies;
        }

        private static bool AddCookie(UnityWebRequest self, string cookieString) {
            if (self == null || cookieString.IsNullOrEmpty()) { return false; }
            // from Response.cs:
            if (cookieString.IndexOf("domain=", StringComparison.CurrentCultureIgnoreCase) == -1) { cookieString += "; domain=" + self.url; }
            if (cookieString.IndexOf("path=", StringComparison.CurrentCultureIgnoreCase) == -1) { cookieString += "; path=" + self.url; }
            return IoC.inject.Get<CookieJar>(self).SetCookie(new Cookie(cookieString));
        }

    }
}
