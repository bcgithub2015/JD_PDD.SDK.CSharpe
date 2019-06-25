﻿using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System;
using Flurl.Http;
using Newtonsoft.Json;

namespace Tool
{
    class JdProgram
    {
        static async Task Main()
        {
            Console.WriteLine("1-京东");
            Console.WriteLine("2-拼多多");
            var type = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine("获取全部接口");

            if (type.KeyChar == '1')
            {
                var allApiJson = "https://union.jd.com/api/apiDoc/categoryList"
                           .PostStringAsync("{}").Result
                           .Content.ReadAsStringAsync().Result;

                var allApi = JsonConvert.DeserializeObject<JdAllApi>(allApiJson);
                foreach (var item in allApi.Data[0].Apis)
                {
                    var code = await GetJdCode(item.ApiId);
                    Directory.CreateDirectory("../Jd.Sdk/Apis");
                    var className = code.FirstOrDefault(x => x.Trim().StartsWith("public class ") && x.EndsWith("Request : JdBaseRequest"));
                    if (className != default)
                    {
                        var flatCode = FlatCode(code);
                        var apiName = className.Trim().Split(' ')[2].Replace("Request", "");
                        File.WriteAllLines($"../Jd.Sdk/Apis/{apiName}.cs", flatCode);
                        Console.WriteLine();
                        Console.WriteLine("[Fact]");
                        Console.WriteLine($"public async void Test_{apiName}()");
                        Console.WriteLine("{");
                        Console.WriteLine($"var req = new {apiName}Request(_appKey, _appSecret)");
                        Console.WriteLine("{");
                        Console.WriteLine("// todo 参数");
                        Console.WriteLine("};");
                        Console.WriteLine("var res = await req.InvokeAsync();");
                        Console.WriteLine("_output.WriteLine(JsonConvert.SerializeObject(req.DebugInfo, Formatting.Indented));");
                        Console.WriteLine("Assert.True(res.Code == 200);");
                        Console.WriteLine("}");
                    }
                    // Console.WriteLine("SUCCESS：" + item.ApiName);
                }
            }
            else if (type.KeyChar == '2')
            {
                // var code = await GetPddCode(key.Trim());
            }
            Console.WriteLine("回车结束...");
            Console.ReadLine();
        }

        static async Task<string[]> GetJdCode(int id)
        {
            var sender = await $"https://union.jd.com/api/apiDoc/apiDocInfo?apiId={id}".PostJsonAsync(new object());
            var @string = await sender.Content.ReadAsStringAsync();
            var root = JsonConvert.DeserializeObject<JdRootObject>(@string);

            var className = string.Join("", root.Data.ApiName.Split('.').Select(UpperFirst));
            var code = new StringBuilder();
            code.AppendLine("using System.Threading.Tasks;");
            code.AppendLine("using Newtonsoft.Json;");
            code.AppendLine();
            code.AppendLine("namespace Jd.Sdk.Apis");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>");
            code.AppendLine($"    /// {root.Data.Caption}--请求参数");
            code.AppendLine($"    /// {root.Data.Description.Trim()}");
            code.AppendLine($"    /// {root.Data.ApiName.Trim()}");
            code.AppendLine($"    /// https://union.jd.com/openplatform/api/{id}");
            code.AppendLine("    /// </summary>");
            code.AppendLine($"    public class {className}Request : JdBaseRequest");
            code.AppendLine("    {");
            code.AppendLine($"        public {className}Request() {{ }}");
            code.AppendLine();
            code.AppendLine($"        public {className}Request(string appKey, string appSecret, string accessToken = null) : base(appKey, appSecret, accessToken) {{ }}");
            code.AppendLine();
            code.AppendLine($"        protected override string Method => \"{root.Data.ApiName}\";");
            code.AppendLine();
            var firstReq = root.Data.PLists.FirstOrDefault(x => x.DataType.EndsWith("Req"));
            if (firstReq != null)
            {
                code.AppendLine($"        protected override string ParamName => \"{firstReq.DataName}\";");
                code.AppendLine();
            }
            else
            {
                firstReq = root.Data.PLists.First(x => x.DataName != "ROOT");
                code.AppendLine($"        protected override string ParamName => \"{firstReq.DataName}\";");
                code.AppendLine();
                code.AppendLine($"        protected override object Param => {UpperFirst(firstReq.DataName)};");
                code.AppendLine();
                code.AppendLine("        /// <summary>");
                code.AppendLine($"        /// {(firstReq.IsRequired.Trim() == "true" ? "必填" : "不必填")}");
                code.AppendLine($"        /// 描述：{firstReq.Description.Trim()}");
                code.AppendLine($"        /// 例如：{firstReq.SampleValue.Trim()}");
                code.AppendLine("        /// </summary>");
                code.AppendLine($"        public {SwitchType(firstReq.DataType)} {UpperFirst(firstReq.DataName)} {{ get; set; }}");
                code.AppendLine();
            }

            var isPage = root.Data.SLists.Any(x => x.DataName == "hasMore")
                ? "JdBasePageResponse"
                : "JdBaseResponse";
            var firstData = root.Data.SLists.FirstOrDefault(x => x.DataName == "data");
            var isArray = string.Empty;
            if (firstData != default)
            {
                isArray = firstData.DataType.EndsWith("[]")
                    ? $"<{className}Response[]>"
                    : $"<{className}Response>";
            }
            else
            {

            }
            code.AppendLine($"        public async Task<{isPage}{isArray}> InvokeAsync()");
            code.AppendLine($"            => await PostAsync<{isPage}{isArray}>();");
            code.AppendLine();

            foreach (var item in root.Data.PLists.Where(x => x.DataName != "ROOT" && x.DataName != firstReq.DataName))
            {
                GetJdParamStereoscopic(item, root.Data.PLists, className, "        ", true, ref code);
            }

            code.AppendLine("    }");
            code.AppendLine();
            code.AppendLine();
            code.AppendLine();

            if (firstData != default)
            {

                code.AppendLine("    /// <summary>");
                code.AppendLine($"    /// {root.Data.Caption}--响应参数");
                code.AppendLine($"    /// {root.Data.Description.Trim()}");
                code.AppendLine($"    /// {root.Data.ApiName.Trim()}");
                code.AppendLine("    /// </summary>");
                code.AppendLine($"    public class {className}Response");
                code.AppendLine("    {");
                foreach (var item in root.Data.SLists.Where(x => x.ParentId == firstData.NodeId))
                {
                    GetJdParamStereoscopic(item, root.Data.SLists, className, "        ", false, ref code);
                }
                code.AppendLine("    }");
            }
            else
            {
                code.AppendLine("    //--------------------------------------");
                code.AppendLine("    // 返回值没有data内容，直接使用基础响应基类");
                code.AppendLine("    //--------------------------------------");
            }
            code.AppendLine("}");
            return code.ToString().Split("\r\n");
        }

        static void GetJdParamStereoscopic(
            JdPlist item,
            JdPlist[] all,
            string className,
            string spacing,
            bool isRequest,
            ref StringBuilder code)
        {
            code.AppendLine($"{spacing}/// <summary>");
            if (isRequest)
            {
                code.AppendLine($"{spacing}/// {(item.IsRequired.Trim() == "true" ? "必填" : "不必填")}");
            }
            code.AppendLine($"{spacing}/// 描述：{item.Description}");
            code.AppendLine($"{spacing}/// 例如：{item.SampleValue}");
            code.AppendLine($"{spacing}/// </summary>");

            var currentNodes = all.Where(x => x.ParentId == item.NodeId).ToArray();

            if (currentNodes.Any())
            {
                var tempName = item.DataType.Replace("[]", "").ToLower();
                var tempSymbol = item.DataType.EndsWith("[]") ? "[]" : "";
                code.AppendLine($"{spacing}public {className}_{UpperFirst(tempName)}{tempSymbol} {UpperFirst(item.DataName)} {{ get; set; }}");
                code.AppendLine($"{spacing}/// <summary>");
                code.AppendLine($"{spacing}/// {item.Description}");
                code.AppendLine($"{spacing}/// </summary>");
                code.AppendLine($"{spacing}public class {className}_{UpperFirst(tempName)}");
                code.AppendLine($"{spacing}{{");
                foreach (var that in currentNodes)
                {
                    GetJdParamStereoscopic(that, all, className, spacing + "    ", isRequest, ref code);
                }
                code.AppendLine($"{spacing}}}");
            }
            else
            {
                if (isRequest)
                {
                    code.AppendLine($"{spacing}[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]");
                }
                code.AppendLine($"{spacing}public {SwitchType(item.DataType)} {UpperFirst(item.DataName)} {{ get; set; }}");
            }
        }

        static async Task<string[]> GetPddCode(string id)
        {
            var sender = await "https://open-api.pinduoduo.com/pop/doc/info/get".PostJsonAsync(new { id });

            var @string = await sender.Content.ReadAsStringAsync();
            var root = JsonConvert.DeserializeObject<JdPddRootobject>(@string);

            var className = root.result.scopeName;
            className = string.Join("", className.Split('.').Select(UpperFirst));
            Console.WriteLine(className);
            var code = new StringBuilder();
            code.AppendLine("namespace todo");
            code.AppendLine("{");
            code.AppendLine("/// <summary>");
            code.AppendLine($"/// {root.result.apiName}--{root.result.usageScenarios}--请求参数");
            code.AppendLine($"/// {root.result.scopeName}");
            code.AppendLine("/// </summary>");
            code.AppendLine($"public class {className}Request : BaseRequest");
            code.AppendLine("{");
            foreach (var item in root.result.requestParamList.Where(x => x.parentId == 0))
            {
                GetPddParamStereoscopic(item, root.result.requestParamList, className, ref code);
            }
            code.AppendLine("}");
            code.AppendLine();
            code.AppendLine();
            code.AppendLine();
            code.AppendLine("/// <summary>");
            code.AppendLine($"/// {root.result.apiName}--{root.result.usageScenarios}--响应参数");
            code.AppendLine($"/// {root.result.scopeName}");
            code.AppendLine("/// </summary>");
            code.AppendLine($"public class {className}Response");
            code.AppendLine("{");
            foreach (var item in root.result.responseParamList.Where(x => x.parentId == 0))
            {
                GetPddParamStereoscopic(
                    new JdRequestparamlist(item),
                    root.result.responseParamList.Select(x => new JdRequestparamlist(x)).ToArray(),
                    className,
                    ref code);
            }
            code.AppendLine("}");
            code.AppendLine("}");
            var lines = code.ToString().Split("\r\n");
            return lines.ToArray();
        }

        static void GetPddParamStereoscopic(
            JdRequestparamlist item,
            JdRequestparamlist[] all,
            string className,
            ref StringBuilder code)
        {
            code.AppendLine("/// <summary>");
            code.AppendLine($"/// 描述：{item.paramDesc}");
            if (item.isMust != -1)
            {
                code.AppendLine($"/// 必填：{item.isMust}");
                code.AppendLine($"/// 默认值：{item.defaultValue}");
            }
            code.AppendLine($"/// 例如：{item.example}");
            code.AppendLine("/// </summary>");

            var currentNodes = all.Where(x => x.parentId == item.id).ToArray();

            if (currentNodes.Any())
            {
                var tempName = string.Join("", item.paramName.Split('_').Select(UpperFirst));
                var tempSymbol = item.paramType.Replace("OBJECT", "");
                code.AppendLine($"public {className}_{tempName}{tempSymbol} {item.paramName} {{ get; set; }}");
                code.AppendLine("/// <summary>");
                code.AppendLine($"/// {item.paramDesc}");
                code.AppendLine("/// </summary>");
                code.AppendLine($"public class {className}_{tempName}");
                code.AppendLine("{");
                foreach (var that in currentNodes)
                {
                    GetPddParamStereoscopic(that, all, className, ref code);
                }
                code.AppendLine("}");
            }
            else
            {
                code.AppendLine($"public {SwitchType(item.paramType)} {item.paramName} {{ get; set; }}");
            }
        }

        static string[] FlatCode(string[] source)
        {
            var copy = new List<string>(source.AsEnumerable());
            var nestClass = source.Where(x => GetStartContinuous(x, ' ').Length >= 8 && x.Trim().StartsWith("public class")).ToArray();
            var toEnds = new List<string[]>();
            foreach (var item in nestClass.OrderByDescending(x => GetStartContinuous(x, ' ').Length))
            {
                var start = copy
                            .ToList()
                            .IndexOf(item)
                            - 3;
                var end = copy
                            .Skip(start).ToList()
                            .IndexOf(GetStartContinuous(item, ' ') + "}")
                            + 1;
                toEnds.Add(copy.Skip(start).Take(end).ToArray());
                copy.RemoveRange(start, end);
            }
            copy.RemoveAt(copy.Count - 2);
            foreach (var item in toEnds)
            {
                var allLength = item.Select(x => GetStartContinuous(x, ' ').Length)
                                    .Where(x => x > 4)
                                    .ToArray();
                var max = allLength.Max();
                var min = allLength.Min();
                var temp = item.Select((x, i) =>
                {
                    var l = GetStartContinuous(x, ' ').Length;
                    if (l == min)
                        return "    " + x.Trim();
                    else if (l == max)
                        return "        " + x.Trim();
                    return default;
                }).Where(x => x != default);
                copy.AddRange(temp);
            }
            copy.Add("}");
            copy.Add("");
            return copy.ToArray();
        }

        static string GetStartContinuous(string source, char @char)
        {
            var result = string.Empty;
            foreach (var c in source)
            {
                if (c != @char) break;
                result += c;
            }
            return result;
        }

        static string UpperFirst(string that)
        {
            return that[0].ToString().ToUpper() + that.Substring(1);
        }

        static string SwitchType(string type)
        {
            switch (type.ToLower())
            {
                case "bool":
                case "boolean": return "bool?";
                case "int":
                case "integer": return "int?";
                case "long": return "long?";
                case "doubel": return "doubel?";
                case "map": return "System.Collections.Generic.Dictionary<string, object>";
                default: return type.ToLower();
            }
        }
    }
}