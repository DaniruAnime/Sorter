using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sorter
{
    public static class DataIO
    {
        private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace DocumentRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static List<double> ReadXlsx(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Файл не найден.", path);

            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
                ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
                if (workbookEntry == null || relsEntry == null)
                    throw new InvalidDataException("Файл не похож на корректную книгу XLSX.");

                XDocument workbook = LoadXml(workbookEntry);
                XDocument rels = LoadXml(relsEntry);
                if (workbook.Root == null || rels.Root == null)
                    throw new InvalidDataException("Не удалось прочитать XML-структуру XLSX.");

                XElement sheetsElement = workbook.Root.Element(SpreadsheetNs + "sheets");
                XElement sheet = sheetsElement == null
                    ? null
                    : sheetsElement.Elements(SpreadsheetNs + "sheet").FirstOrDefault();
                if (sheet == null)
                    throw new InvalidDataException("В книге XLSX нет листов.");

                XAttribute relationId = sheet.Attribute(DocumentRelNs + "id");
                if (relationId == null)
                    throw new InvalidDataException("Не удалось определить первый лист XLSX.");

                XElement relation = rels.Root
                    .Elements(PackageRelNs + "Relationship")
                    .FirstOrDefault(relationshipElement => (string)relationshipElement.Attribute("Id") == relationId.Value);
                if (relation == null)
                    throw new InvalidDataException("Не удалось найти связь с первым листом XLSX.");

                string target = (string)relation.Attribute("Target");
                if (string.IsNullOrEmpty(target))
                    throw new InvalidDataException("Не указан путь к листу XLSX.");

                target = NormalizeZipPath(target);
                ZipArchiveEntry sheetEntry = archive.GetEntry(target);
                if (sheetEntry == null)
                    throw new InvalidDataException("Файл первого листа XLSX не найден: " + target);

                XDocument sheetDocument = LoadXml(sheetEntry);
                List<double> result = new List<double>();

                foreach (XElement cell in sheetDocument.Descendants(SpreadsheetNs + "c"))
                {
                    string type = (string)cell.Attribute("t") ?? "";
                    string text = null;

                    if (type == "inlineStr")
                    {
                        XElement isElement = cell.Element(SpreadsheetNs + "is");
                        if (isElement != null)
                            text = isElement.Value;
                    }
                    else
                    {
                        XElement value = cell.Element(SpreadsheetNs + "v");
                        if (value != null)
                        {
                            text = value.Value;
                            if (type == "s")
                            {
                                int index;
                                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                                    && index >= 0 && index < sharedStrings.Count)
                                    text = sharedStrings[index];
                            }
                        }
                    }

                    double parsed;
                    if (!string.IsNullOrWhiteSpace(text)
                        && TryParseNumber(text, out parsed)
                        && !double.IsNaN(parsed)
                        && !double.IsInfinity(parsed))
                        result.Add(parsed);
                }

                if (result.Count == 0)
                    throw new InvalidDataException("В первом листе XLSX не найдено числовых значений.");
                return result;
            }
        }

        private static string NormalizeZipPath(string target)
        {
            string normalizedTarget = target.Replace('\\', '/').TrimStart('/');
            if (!normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                normalizedTarget = "xl/" + normalizedTarget;

            string[] parts = normalizedTarget.Split('/');
            List<string> pathParts = new List<string>();
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part) || part == ".")
                    continue;
                if (part == "..")
                {
                    if (pathParts.Count > 0)
                        pathParts.RemoveAt(pathParts.Count - 1);
                    continue;
                }
                pathParts.Add(part);
            }

            return string.Join("/", pathParts);
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            List<string> result = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return result;

            XDocument document = LoadXml(entry);
            foreach (XElement item in document.Descendants(SpreadsheetNs + "si"))
                result.Add(item.Value);
            return result;
        }

        public static List<double> ReadGoogleSheet(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL Google Sheets пуст.");

            string csvUrl = BuildGoogleCsvUrl(url.Trim());
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "SortingVisualizerMono/1.0";
                string csv = client.DownloadString(csvUrl);
                List<double> result = ParseCsvNumbers(csv);
                if (result.Count == 0)
                    throw new InvalidDataException("Google Sheets не вернул числовых значений. Проверьте доступ к таблице и диапазон.");
                return result;
            }
        }

        private static string BuildGoogleCsvUrl(string input)
        {
            if (input.IndexOf("format=csv", StringComparison.OrdinalIgnoreCase) >= 0 || input.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return input;

            Match idMatch = Regex.Match(input, @"/spreadsheets/d/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
            if (!idMatch.Success)
                throw new ArgumentException("Не найден идентификатор Google таблицы в URL.");

            string gid = "0";
            Match gidMatch = Regex.Match(input, @"(?:[?#&])gid=([0-9]+)", RegexOptions.IgnoreCase);
            if (gidMatch.Success) gid = gidMatch.Groups[1].Value;

            return "https://docs.google.com/spreadsheets/d/" + idMatch.Groups[1].Value + "/export?format=csv&gid=" + gid;
        }

        private static List<double> ParseCsvNumbers(string csv)
        {
            List<double> result = new List<double>();
            foreach (string field in ParseCsv(csv))
            {
                double number;
                if (TryParseNumber(field, out number)
                    && !double.IsNaN(number)
                    && !double.IsInfinity(number))
                    result.Add(number);
            }
            return result;
        }

        private static List<string> ParseCsv(string input)
        {
            List<string> fields = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int position = 0; position < input.Length; ++position)
            {
                char currentCharacter = input[position];
                if (inQuotes)
                {
                    if (currentCharacter == '"')
                    {
                        if (position + 1 < input.Length && input[position + 1] == '"')
                        {
                            current.Append('"');
                            ++position;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(currentCharacter);
                    }
                }
                else
                {
                    if (currentCharacter == '"') inQuotes = true;
                    else if (currentCharacter == ',' || currentCharacter == '\n' || currentCharacter == '\r')
                    {
                        fields.Add(current.ToString().Trim());
                        current.Clear();
                        if (currentCharacter == '\r' && position + 1 < input.Length && input[position + 1] == '\n') ++position;
                    }
                    else current.Append(currentCharacter);
                }
            }
            fields.Add(current.ToString().Trim());
            return fields;
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
                return XDocument.Load(stream, LoadOptions.None);
        }

        private static bool TryParseNumber(string text, out double value)
        {
            string normalized = (text ?? "").Trim().Replace('\u00A0', ' ');
            return double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out value)
                || double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out value)
                || double.TryParse(normalized.Replace(',', '.'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value);
        }
    }
}
