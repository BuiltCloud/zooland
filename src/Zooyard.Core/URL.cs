﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Zooyard.Core.Utils;

namespace Zooyard.Core
{
    public sealed class URL
    {
        public const string BACKUP_KEY = "backup";
        public const string DEFAULT_KEY_PREFIX = "default.";
        public static readonly Regex COMMA_SPLIT_PATTERN = new Regex("\\s*[,]+\\s*", RegexOptions.Compiled);
        public const string LOCALHOST_KEY = "localhost";
        public const string ANYHOST_KEY = "anyhost";
        public const string ANYHOST_VALUE = "0.0.0.0";
        public const string GROUP_KEY = "group";
        public const string VERSION_KEY = "version";
        public const string INTERFACE_KEY = "interface";

        //private readonly IDictionary<string, string> parameters;

        // ==== cache ====

        private volatile IDictionary<string, IConvertible> numbers;//Number to long
        private volatile IDictionary<string, URL> urls;
        private volatile string ip;
        private volatile string full;
        private volatile string identity;
        private volatile string parameter;
        private volatile string @string;

        internal URL()
        {
            this.Protocol = null;
            this.Username = null;
            this.Password = null;
            this.Host = null;
            this.Port = 0;
            this.Path = null;
            this.Parameters = null;
        }

        public URL(string protocol, string host, int port)
            : this(protocol, null, null, host, port, null, (IDictionary<string, string>)null)
        {
        }

        //public URL(string protocol, string host, int port, string[] pairs)
        //    : this(protocol, null, null, host, port, null, CollectionUtils.toStringMap(pairs)) // 变长参数...与下面的path参数冲突，改为数组
        //{
        //}

        public URL(string protocol, string host, int port, IDictionary<string, string> parameters)
            : this(protocol, null, null, host, port, null, parameters)
        {
        }

        public URL(string protocol, string host, int port, string path)
            : this(protocol, null, null, host, port, path, null)
        {
        }

        //public URL(string protocol, string host, int port, string path, params string[] pairs)
        //    : this(protocol, null, null, host, port, path, CollectionUtils.toStringMap(pairs))
        //{
        //}

        public URL(string protocol, string host, int port, string path, IDictionary<string, string> parameters)
            : this(protocol, null, null, host, port, path, parameters)
        {
        }

        public URL(string protocol, string username, string password, string host, int port, string path)
            : this(protocol, username, password, host, port, path, null)
        {
        }

        //public URL(string protocol, string username, string password, string host, int port, string path, params string[] pairs)
        //    : this(protocol, username, password, host, port, path, CollectionUtils.toStringMap(pairs))
        //{
        //}

        public URL(string protocol, string username, string password, string host, int port, string path, IDictionary<string, string> parameters)
        {
            if ((username == null || username.Length == 0) && password != null && password.Length > 0)
            {
                throw new System.ArgumentException("Invalid url, password without username!");
            }
            this.Protocol = protocol;
            this.Username = username;
            this.Password = password;
            this.Host = host;
            this.Port = (port < 0 ? 0 : port);
            // trim the beginning "/"
            while (path != null && path.StartsWith("/"))
            {
                path = path.Substring(1);
            }
            this.Path = path;
            if (parameters == null)
            {
                parameters = new Dictionary<string, string>();
            }
            else
            {
                parameters = new Dictionary<string, string>(parameters);
            }
            this.Parameters = parameters;// Collections.unmodifiableMap(parameters);
        }

        /// <summary>
        /// Parse url string
        /// </summary>
        /// <param name="url"> URL string </param>
        /// <returns> URL instance </returns>
        /// <seealso cref= URL </seealso>
        public static URL valueOf(string url)
        {
            if (url == null || (url = url.Trim()).Length == 0)
            {
                throw new System.ArgumentException("url == null");
            }
            string protocol = null;
            string username = null;
            string password = null;
            string host = null;
            int port = 0;
            string path = null;
            IDictionary<string, string> parameters = null;
            int i = url.IndexOf("?"); // seperator between body and parameters
            if (i >= 0)
            {
                string[] parts = url.Substring(i + 1).Split(new[] { "&" }, StringSplitOptions.RemoveEmptyEntries);
                parameters = new Dictionary<string, string>();
                foreach (string part in parts)
                {
                    var partinner = part.Trim();
                    if (partinner.Length > 0)
                    {
                        int j = partinner.IndexOf('=');
                        if (j >= 0)
                        {
                            parameters[partinner.Substring(0, j)] = partinner.Substring(j + 1);
                        }
                        else
                        {
                            parameters[part] = partinner;
                        }
                    }
                }
                url = url.Substring(0, i);
            }
            i = url.IndexOf("://");
            if (i >= 0)
            {
                if (i == 0)
                {
                    throw new Exception("url missing protocol: \"" + url + "\"");
                }
                protocol = url.Substring(0, i);
                url = url.Substring(i + 3);
            }
            else
            {
                i = url.IndexOf(":/");
                if (i >= 0)
                {
                    if (i == 0)
                    {
                        throw new Exception("url missing protocol: \"" + url + "\"");
                    }
                    protocol = url.Substring(0, i);
                    url = url.Substring(i + 1);
                }
            }

            i = url.IndexOf("/");
            if (i >= 0)
            {
                path = url.Substring(i + 1);
                url = url.Substring(0, i);
            }
            i = url.IndexOf("@");
            if (i >= 0)
            {
                username = url.Substring(0, i);
                int j = username.IndexOf(":");
                if (j >= 0)
                {
                    password = username.Substring(j + 1);
                    username = username.Substring(0, j);
                }
                url = url.Substring(i + 1);
            }
            i = url.IndexOf(":");
            if (i >= 0 && i < url.Length - 1)
            {
                port = Convert.ToInt32(url.Substring(i + 1));
                url = url.Substring(0, i);
            }
            if (url.Length > 0)
            {
                host = url;
            }
            return new URL(protocol, username, password, host, port, path, parameters);
        }

        public string Protocol { get; }

        public string Username { get; }

        public string Password { get; }

        public string Authority
        {
            get
            {
                if ((Username == null || Username.Length == 0) && (Password == null || Password.Length == 0))
                {
                    return null;
                }
                return (Username == null ? "" : Username) + ":" + (Password == null ? "" : Password);
            }
        }

        public string Host { get; }

        /// <summary>
        /// 获取IP地址.
        /// 
        /// 请注意：
        /// 如果和Socket的地址对比，
        /// 或用地址作为Map的Key查找，
        /// 请使用IP而不是Host，
        /// 否则配置域名会有问题
        /// </summary>
        /// <returns> ip </returns>
        public string Ip
        {
            get
            {
                if (ip == null)
                {
                    ip = NetUtils.getIpByHost(Host);
                }
                return ip;
            }
        }

        public int Port { get; }

        public int GetPort(int defaultPort)
        {
            return Port <= 0 ? defaultPort : Port;
        }

        public string Address
        {
            get
            {
                return Port <= 0 ? Host : Host + ":" + Port;
            }
        }

        public string BackupAddress
        {
            get
            {
                return GetBackupAddress(0);
            }
        }

        public string GetBackupAddress(int defaultPort)
        {
            var address = new StringBuilder(appendDefaultPort(Address, defaultPort));
            var backups = GetParameter(BACKUP_KEY, new string[0]);
            if (backups != null && backups.Length > 0)
            {
                foreach (var backup in backups)
                {
                    address.Append(",");
                    address.Append(appendDefaultPort(backup, defaultPort));
                }
            }
            return address.ToString();
        }

        public IList<URL> BackupUrls
        {
            get
            {
                var urls = new List<URL>();
                urls.Add(this);
                string[] backups = GetParameter(BACKUP_KEY, new string[0]);
                if (backups != null && backups.Length > 0)
                {
                    foreach (string backup in backups)
                    {
                        urls.Add(this.SetAddress(backup));
                    }
                }
                return urls;
            }
        }

        private string appendDefaultPort(string address, int defaultPort)
        {
            if (!string.IsNullOrEmpty(address) && defaultPort > 0)
            {
                int i = address.IndexOf(':');
                if (i < 0)
                {
                    return address + ":" + defaultPort;
                }
                else if (Convert.ToInt32(address.Substring(i + 1)) == 0)
                {
                    return address.Substring(0, i + 1) + defaultPort;
                }
            }
            return address;
        }

        public string Path { get; }

        public string AbsolutePath
        {
            get
            {
                if (Path != null && !Path.StartsWith("/"))
                {
                    return "/" + Path;
                }
                return Path;
            }
        }

        public URL SetProtocol(string protocol)
        {
            return new URL(protocol, Username, Password, Host, Port, Path, Parameters);
        }

        public URL SetUsername(string username)
        {
            return new URL(Protocol, username, Password, Host, Port, Path, Parameters);
        }

        public URL SetPassword(string password)
        {
            return new URL(Protocol, Username, password, Host, Port, Path, Parameters);
        }

        public URL SetAddress(string address)
        {
            int i = address.LastIndexOf(':');
            string host;
            int port = this.Port;
            if (i >= 0)
            {
                host = address.Substring(0, i);
                port = Convert.ToInt32(address.Substring(i + 1));
            }
            else
            {
                host = address;
            }
            return new URL(Protocol, Username, Password, host, port, Path, Parameters);
        }

        public URL SetHost(string host)
        {
            return new URL(Protocol, Username, Password, host, Port, Path, Parameters);
        }

        public URL SetPort(int port)
        {
            return new URL(Protocol, Username, Password, Host, port, Path, Parameters);
        }

        public URL SetPath(string path)
        {
            return new URL(Protocol, Username, Password, Host, Port, path, Parameters);
        }

        public IDictionary<string, string> Parameters { get; private set; }

        public string GetParameterAndDecoded(string key)
        {
            return GetParameterAndDecoded(key, null);
        }

        public string GetParameterAndDecoded(string key, string defaultValue)
        {
            return Decode(GetParameter(key, defaultValue));
        }

        public string GetParameter(string key)
        {
            if (this.Parameters.ContainsKey(key))
            {
                return this.Parameters[key];
            }

            if (this.Parameters.ContainsKey(DEFAULT_KEY_PREFIX + key))
            {
                return this.Parameters[DEFAULT_KEY_PREFIX + key];
            }
            return null;
        }

        public string GetParameter(string key, string defaultValue)
        {
            string value = GetParameter(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return value;
        }

        public string[] GetParameter(string key, string[] defaultValue)
        {
            string value = GetParameter(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return COMMA_SPLIT_PATTERN.Split(value);
        }

        private IDictionary<string, IConvertible> Numbers
        {
            get
            {
                if (numbers == null) // 允许并发重复创建
                {
                    numbers = new ConcurrentDictionary<string, IConvertible>();
                }
                return numbers;
            }
        }

        private IDictionary<string, URL> Urls
        {
            get
            {
                if (urls == null) // 允许并发重复创建
                {
                    urls = new ConcurrentDictionary<string, URL>();
                }
                return urls;
            }
        }

        public URL GetUrlParameter(string key)
        {
            if (Urls.ContainsKey(key))
            {
                return Urls[key];
            }
            string value = GetParameterAndDecoded(key);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            var u = URL.valueOf(value);
            Urls.Add(key, u);
            return u;
        }

        public T GetParameter<T>(string key, T defaultValue = default(T)) where T : IConvertible
        {
            if (Numbers.ContainsKey(key))
            {
                return (T)Numbers[key];
            }
            var value = GetParameter(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            T b = (T)value.ChangeType(typeof(T));
            Numbers.Add(key, b);
            return b;
        }

        public T GetPositiveParameter<T>(string key, T defaultValue) where T:IComparable<int>,IConvertible
        {
            if (defaultValue.CompareTo(0)<=0)
            {
                throw new ArgumentException("defaultValue <= 0");
            }
            var value = GetParameter<T>(key, defaultValue);
            if (value.CompareTo(0) <= 0)
            {
                return defaultValue;
            }
            return value;
        }


        public char GetParameter(string key, char defaultValue)
        {
            string value = GetParameter(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return value[0];
        }

        public bool GetParameter(string key, bool defaultValue)
        {
            string value = GetParameter(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return Convert.ToBoolean(value);
        }

        public bool HasParameter(string key)
        {
            var value = GetParameter(key);
            return !string.IsNullOrEmpty(value);
        }

        public string GetMethodParameterAndDecoded(string method, string key)
        {
            return URL.Decode(GetMethodParameter(method, key));
        }

        public string GetMethodParameterAndDecoded(string method, string key, string defaultValue)
        {
            return URL.Decode(GetMethodParameter(method, key, defaultValue));
        }

        public string GetInterfaceParameter(string interfaceName, string key)
        {
            var interfaceKey = $"interface.{interfaceName}.{key}";
            var value = "";
            if (this.Parameters.ContainsKey(interfaceKey))
            {
                value = this.Parameters[interfaceKey];
            }
            if (string.IsNullOrEmpty(value))
            {
                return GetParameter(key);
            }
            return value;
        }

        public string GetInterfaceParameter(string interfaceName, string key, string defaultValue)
        {
            string value = GetInterfaceParameter(interfaceName, key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return value;
        }


        public string GetMethodParameter(string method, string key)
        {
            var methodKey = method + "." + key;
            var value = "";
            if (this.Parameters.ContainsKey(methodKey))
            {
                value = this.Parameters[methodKey];
            }
            if (string.IsNullOrEmpty(value))
            {
                return GetParameter(key);
            }
            return value;
        }

        public string GetMethodParameter(string method, string key, string defaultValue)
        {
            string value = GetMethodParameter(method, key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return value;
        }

        public T GetMethodParameter<T>(string method, string key, T defaultValue=default(T)) where T:IConvertible
        {
            var methodKey = method + "." + key;
            if (Numbers.ContainsKey(methodKey))
            {
                return (T)Numbers[methodKey];
            }
            var value = GetMethodParameter(method, key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            T d = (T)value.ChangeType(typeof(T));
            Numbers.Add(methodKey, d);
            return d;
        }

        public T GetMethodPositiveParameter<T>(string method, string key, T defaultValue) where T : IComparable<int>, IConvertible
        {
            if (defaultValue.CompareTo(0) <= 0)
            {
                throw new ArgumentException("defaultValue <= 0");
            }
            var value = GetMethodParameter<T>(method, key, defaultValue);
            if (value.CompareTo(0) <= 0)
            {
                return defaultValue;
            }
            return value;
        }

        public char GetMethodParameter(string method, string key, char defaultValue)
        {
            var value = GetMethodParameter(method, key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return value[0];
        }

        public bool GetMethodParameter(string method, string key, bool defaultValue)
        {
            var value = GetMethodParameter(method, key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return Convert.ToBoolean(value);
        }

        public bool HasMethodParameter(string method, string key)
        {
            if (method == null)
            {
                var suffix = "." + key;
                foreach (var fullKey in this.Parameters.Keys)
                {
                    if (fullKey.EndsWith(suffix))
                    {
                        return true;
                    }
                }
                return false;
            }
            if (key == null)
            {
                var prefix = method + ".";
                foreach (var fullKey in this.Parameters.Keys)
                {
                    if (fullKey.StartsWith(prefix))
                    {
                        return true;
                    }
                }
                return false;
            }
            var value = GetMethodParameter(method, key);
            return value != null && value.Length > 0;
        }

        public bool LocalHost
        {
            get
            {
                return NetUtils.isLocalHost(Host) || GetParameter(LOCALHOST_KEY, false);
            }
        }

        public bool AnyHost
        {
            get
            {
                return ANYHOST_VALUE.Equals(Host) || GetParameter(ANYHOST_KEY, false);
            }
        }

        public URL AddParameterAndEncoded(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return this;
            }
            return AddParameter(key, Encode(value));
        }
        
        public URL AddParameter<T>(string key, T value)
        {
            return AddParameter(key, Convert.ToString(value));
        }
        public URL AddParameter(string key, Enum value)
        {
            if (value == null)
            {
                return this;
            }
            return AddParameter(key, value.ToString());
        }

        public URL AddParameter(string key, double? value)
        {
            if (value == null)
            {
                return this;
            }
            return AddParameter(key, value.ToString());
        }

        public URL AddParameter(string key, char[] value)
        {
            if (value == null || value.Length == 0)
            {
                return this;
            }
            return AddParameter(key, Convert.ToString(value));
        }

        public URL AddParameter(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                return this;
            }
            //如果没有修改，直接返回。
            if (Parameters.ContainsKey(key) && Parameters[key] == value) // value != null
            {
                return this;
            }

            var map = new Dictionary<string, string>(Parameters);
            map[key] = value;
            return new URL(Protocol, Username, Password, Host, Port, Path, map);
        }

        public URL AddParameterIfAbsent(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                return this;
            }
            if (HasParameter(key))
            {
                return this;
            }
            var map = new Dictionary<string, string>(Parameters);
            map[key] = value;
            return new URL(Protocol, Username, Password, Host, Port, Path, map);
        }

        /// <summary>
        /// Add parameters to a new url.
        /// </summary>
        /// <param name="parameters"> </param>
        /// <returns> A new URL  </returns>
        public URL AddParameters(IDictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return this;
            }

            bool hasAndEqual = true;
            foreach (var entry in parameters)
            {
                if (!Parameters.ContainsKey(entry.Key) && entry.Value != null || !Parameters[entry.Key].Equals(entry.Value))
                {
                    hasAndEqual = false;
                    break;
                }
            }
            // 如果没有修改，直接返回。
            if (hasAndEqual)
            {
                return this;
            }

            var map = new Dictionary<string, string>(Parameters);
            map.PutAll(parameters);
            return new URL(Protocol, Username, Password, Host, Port, Path, map);
        }

        public URL AddParametersIfAbsent(IDictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return this;
            }
            var map = new Dictionary<string, string>(parameters);
            map.PutAll(Parameters);
            return new URL(Protocol, Username, Password, Host, Port, Path, map);
        }

        public URL AddParameters(params string[] pairs)
        {
            if (pairs == null || pairs.Length == 0)
            {
                return this;
            }
            if (pairs.Length % 2 != 0)
            {
                throw new System.ArgumentException("Map pairs can not be odd number.");
            }
            var map = new Dictionary<string, string>();
            int len = pairs.Length / 2;
            for (int i = 0; i < len; i++)
            {
                map[pairs[2 * i]] = pairs[2 * i + 1];
            }
            return AddParameters(map);
        }

        public URL AddParameterString(string query)
        {
            if (query == null || query.Length == 0)
            {
                return this;
            }
            return AddParameters(StringUtils.parseQueryString(query));
        }

        public URL RemoveParameter(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return this;
            }
            return RemoveParameters(key);
        }

        public URL RemoveParameters(ICollection<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return this;
            }
            return RemoveParameters(keys.ToArray());
        }

        public URL RemoveParameters(params string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return this;
            }
            var map = new Dictionary<string, string>(Parameters);
            foreach (string key in keys)
            {
                map.Remove(key);
            }
            if (map.Count == Parameters.Count)
            {
                return this;
            }
            return new URL(Protocol, Username, Password, Host, Port, Path, map);
        }

        public URL ClearParameters()
        {
            return new URL(Protocol, Username, Password, Host, Port, Path, new Dictionary<string, string>());
        }

        public string GetRawParameter(string key)
        {
            if ("protocol".Equals(key))
            {
                return Protocol;
            }
            if ("username".Equals(key))
            {
                return Username;
            }
            if ("password".Equals(key))
            {
                return Password;
            }
            if ("host".Equals(key))
            {
                return Host;
            }
            if ("port".Equals(key))
            {
                return Convert.ToString(Port);
            }
            if ("path".Equals(key))
            {
                return Path;
            }
            return GetParameter(key);
        }

        public IDictionary<string, string> ToMap()
        {
            var map = new Dictionary<string, string>(this.Parameters);
            if (Protocol != null)
            {
                map["protocol"] = Protocol;
            }
            if (Username != null)
            {
                map["username"] = Username;
            }
            if (Password != null)
            {
                map["password"] = Password;
            }
            if (Host != null)
            {
                map["host"] = Host;
            }
            if (Port > 0)
            {
                map["port"] = Convert.ToString(Port);
            }
            if (Path != null)
            {
                map["path"] = Path;
            }
            return map;
        }

        public override string ToString()
        {
            if (@string != null)
            {
                return @string;
            }
            return @string = buildString(false, true); // no show username and password
        }

        public string ToString(params string[] parameters)
        {
            return buildString(false, true, parameters); // no show username and password
        }

        public string ToIdentityString()
        {
            if (identity != null)
            {
                return identity;
            }
            return identity = buildString(true, false); // only return identity message, see the method "equals" and "hashCode"
        }

        public string ToIdentityString(params string[] parameters)
        {
            return buildString(true, false, parameters); // only return identity message, see the method "equals" and "hashCode"
        }

        public string ToFullString()
        {
            if (full != null)
            {
                return full;
            }
            return full = buildString(true, true);
        }

        public string ToFullString(params string[] parameters)
        {
            return buildString(true, true, parameters);
        }

        public string ToParameterString()
        {
            if (parameter != null)
            {
                return parameter;
            }
            return parameter = ToParameterString(new string[0]);
        }

        public string ToParameterString(params string[] parameters)
        {
            var buf = new StringBuilder();
            buildParameters(buf, false, parameters);
            return buf.ToString();
        }

        private void buildParameters(StringBuilder buf, bool concat, string[] parameters)
        {
            if (Parameters != null && Parameters.Count > 0)
            {
                IList<string> includes = (parameters == null || parameters.Length == 0 ? null : new List<string>(parameters));
                bool first = true;
                foreach (KeyValuePair<string, string> entry in (new SortedDictionary<string, string>(Parameters)))
                {
                    if (entry.Key != null && entry.Key.Length > 0 && (includes == null || includes.Contains(entry.Key)))
                    {
                        if (first)
                        {
                            if (concat)
                            {
                                buf.Append("?");
                            }
                            first = false;
                        }
                        else
                        {
                            buf.Append("&");
                        }
                        buf.Append(entry.Key);
                        buf.Append("=");
                        buf.Append(entry.Value == null ? "" : entry.Value.Trim());
                    }
                }
            }
        }

        private string buildString(bool appendUser, bool appendParameter, params string[] parameters)
        {
            return buildString(appendUser, appendParameter, false, false, parameters);
        }

        private string buildString(bool appendUser, bool appendParameter, bool useIP, bool useService, params string[] parameters)
        {
            var buf = new StringBuilder();
            if (Protocol != null && Protocol.Length > 0)
            {
                buf.Append(Protocol);
                buf.Append("://");
            }
            if (appendUser && Username != null && Username.Length > 0)
            {
                buf.Append(Username);
                if (Password != null && Password.Length > 0)
                {
                    buf.Append(":");
                    buf.Append(Password);
                }
                buf.Append("@");
            }
            string host;
            if (useIP)
            {
                host = Ip;
            }
            else
            {
                host = Host;
            }
            if (host != null && host.Length > 0)
            {
                buf.Append(host);
                if (Port > 0)
                {
                    buf.Append(":");
                    buf.Append(Port);
                }
            }
            string path;
            if (useService)
            {
                path = ServiceKey;
            }
            else
            {
                path = Path;
            }
            if (path != null && path.Length > 0)
            {
                buf.Append("/");
                buf.Append(path);
            }
            if (appendParameter)
            {
                buildParameters(buf, true, parameters);
            }
            return buf.ToString();
        }

        public DnsEndPoint ToDnsEndPoint()
        {
            return new DnsEndPoint(Host, Port);
        }
        public string ServiceKey
        {
            get
            {
                var inf = ServiceInterface;
                if (inf == null)
                {
                    return null;
                }
                var buf = new StringBuilder();
                var group = GetParameter(GROUP_KEY);
                if (group != null && group.Length > 0)
                {
                    buf.Append(group).Append("/");
                }
                buf.Append(inf);
                var version = GetParameter(VERSION_KEY);
                if (version?.Length > 0)
                {
                    buf.Append(":").Append(version);
                }
                return buf.ToString();
            }
        }

        public string ToServiceString()
        {
            return buildString(true, false, true, true);
        }

        public string ServiceInterface
        {
            get
            {
                return GetParameter(INTERFACE_KEY, Path);
            }
        }

        public URL SetServiceInterface(string service)
        {
            return AddParameter(INTERFACE_KEY, service);
        }

        public static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            try
            {
                return WebUtility.UrlEncode(value);
                //return URLEncoder.encode(value, "UTF-8");
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            try
            {
                return WebUtility.UrlDecode(value);
                //return URLDecoder.decode(value, "UTF-8");
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((Host == null) ? 0 : Host.GetHashCode());
            result = prime * result + ((this.Parameters == null) ? 0 : this.Parameters.GetHashCode());
            result = prime * result + ((Password == null) ? 0 : Password.GetHashCode());
            result = prime * result + ((Path == null) ? 0 : Path.GetHashCode());
            result = prime * result + Port;
            result = prime * result + ((Protocol == null) ? 0 : Protocol.GetHashCode());
            result = prime * result + ((Username == null) ? 0 : Username.GetHashCode());
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null)
            {
                return false;
            }
            if (this.GetType() != obj.GetType())
            {
                return false;
            }
            URL other = (URL)obj;
            if (Host == null)
            {
                if (other.Host != null)
                {
                    return false;
                }
            }
            else if (!Host.Equals(other.Host))
            {
                return false;
            }
            if (this.Parameters == null)
            {
                if (other.Parameters != null)
                {
                    return false;
                }
            }
            else if (!this.Parameters.Equals(other.Parameters))
            {
                return false;
            }
            if (Password == null)
            {
                if (other.Password != null)
                {
                    return false;
                }
            }
            else if (!Password.Equals(other.Password))
            {
                return false;
            }
            if (Path == null)
            {
                if (other.Path != null)
                {
                    return false;
                }
            }
            else if (!Path.Equals(other.Path))
            {
                return false;
            }
            if (Port != other.Port)
            {
                return false;
            }
            if (Protocol == null)
            {
                if (other.Protocol != null)
                {
                    return false;
                }
            }
            else if (!Protocol.Equals(other.Protocol))
            {
                return false;
            }
            if (Username == null)
            {
                if (other.Username != null)
                {
                    return false;
                }
            }
            else if (!Username.Equals(other.Username))
            {
                return false;
            }
            return true;
        }
    }
}
