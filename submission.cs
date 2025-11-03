// submission.cs
// ASU CSE445 – Assignment 4
// Implements: Verification(xmlUrl, xsdUrl) and Xml2Json(xmlUrl)
// Prints test results in Main using remote URLs for Hotels.xml / HotelsErrors.xml / Hotels.xsd
//
// Build target: .NET Framework 4.7, C# 7.0
// External package used: Newtonsoft.Json

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Newtonsoft.Json;

/**
 * This file conforms to the provided template:
 * - Namespace must be ConsoleApp1
 * - Class Program with fields xmlURL, xmlErrorURL, xsdURL
 * - Methods: Main, Verification, Xml2Json
 * Notes:
 * - Replace the three URL strings below with your public GitHub URLs.
 * - Program prints:
 *   (1) Validation result for the good XML  -> "No errors are found"
 *   (2) Validation result for the error XML -> full list of errors with line/pos
 *   (3) JSON converted text for the good XML
 */
namespace ConsoleApp1
{
    public class Program
    {
        // ====== REPLACE THESE WITH YOUR PUBLIC GITHUB URLS ======
        public static string xmlURL      = "https://raw.githubusercontent.com/mohammadhsa/CSE-445_Hotels-XML/main/Hotels.xml";
        public static string xmlErrorURL = "https://raw.githubusercontent.com/mohammadhsa/CSE-445_Hotels-XML/main/HotelsErrors.xml";
        public static string xsdURL      = "https://raw.githubusercontent.com/mohammadhsa/CSE-445_Hotels-XML/main/Hotels.xsd";
        // If you prefer raw links, you can use:
        // "https://raw.githubusercontent.com/<user>/<repo>/<branch>/Hotels.xml", etc.
        // ========================================================

        public static void Main(string[] args)
        {
            // 3(1) – verify the valid XML
            string result = Verification(xmlURL, xsdURL);
            Console.WriteLine("---- Validation: Hotels.xml ----");
            Console.WriteLine(result);
            Console.WriteLine();

            // 3(2) – verify the invalid XML
            result = Verification(xmlErrorURL, xsdURL);
            Console.WriteLine("---- Validation: HotelsErrors.xml ----");
            Console.WriteLine(result);
            Console.WriteLine();

            // 3(3) – convert valid XML to JSON (list of elements under Hotels/Hotel)
            Console.WriteLine("---- Xml2Json(Hotels.xml) ----");
            string json = Xml2Json(xmlURL);
            Console.WriteLine(json);
            Console.WriteLine();

            // Optional self-check: ensure the JSON is consumable by Newtonsoft
            // (not required by the assignment output, but proves correctness)
            try
            {
                var xmlDoc = JsonConvert.DeserializeXmlNode(json, "RootForCheck");
                if (xmlDoc == null) throw new Exception("DeserializeXmlNode returned null.");
                Console.WriteLine("Round-trip check with JsonConvert.DeserializeXmlNode: OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Round-trip check failed: " + ex.Message);
            }
        }

        // Q2.1
        // Validates XML against XSD at the given URLs.
        // Returns "No errors are found" when valid; otherwise, returns all error/warning messages.
        public static string Verification(string xmlUrl, string xsdUrl)
        {
            var messages = new StringBuilder();

            try
            {
                var schemas = new XmlSchemaSet();
                // No target namespace in Hotels.xsd
                schemas.Add(null, xsdUrl);

                var settings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    Schemas = schemas,
                    DtdProcessing = DtdProcessing.Prohibit
                };
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                settings.ValidationEventHandler += (sender, e) =>
                {
                    var ex = e.Exception;
                    string where = (ex != null)
                        ? $" (line {ex.LineNumber}, pos {ex.LinePosition})"
                        : "";
                    messages.AppendLine($"{e.Severity}: {e.Message}{where}");
                };

                // XmlReader can open http/https URLs directly in .NET 4.7
                using (var reader = XmlReader.Create(xmlUrl, settings))
                {
                    while (reader.Read()) { /* streaming validation */ }
                }
            }
            catch (Exception ex)
            {
                messages.AppendLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
            }

            return messages.Length == 0 ? "No errors are found" : messages.ToString().TrimEnd();
        }

        // Q2.2
        // Converts a valid Hotels.xml into the required JSON shape:
        // {
        //   "Hotels": { "Hotel": [ { "Name": "...", "Phone": [...],
        //      "Address": { "Number":"...", ... ,"NearestAirport":"..."}, "_Rating":"4.2" }, ... ] }
        // }
        // The returned jsonText is compatible with JsonConvert.DeserializeXmlNode(jsonText, ...).
        public static string Xml2Json(string xmlUrl)
        {
            XDocument doc = XDocument.Load(xmlUrl);
            if (doc.Root == null || doc.Root.Name.LocalName != "Hotels")
                throw new InvalidDataException("Root element must be <Hotels>.");

            var hotelsArray = new List<object>();

            foreach (var h in doc.Root.Elements("Hotel"))
            {
                // Name (required)
                var nameEl = h.Element("Name");
                if (nameEl == null) throw new InvalidDataException("Hotel missing <Name>.");
                string name = (string)nameEl;

                // Phones (one or more)
                var phones = h.Elements("Phone")
                              .Select(p => ((string)p ?? "").Trim())
                              .Where(s => s.Length > 0)
                              .ToList();
                if (phones.Count == 0)
                    throw new InvalidDataException("Hotel must contain at least one <Phone>.");

                // Address (required) + required subfields + required attribute NearestAirport
                var addrEl = h.Element("Address");
                if (addrEl == null) throw new InvalidDataException("Hotel missing <Address>.");

                var address = new Dictionary<string, string>
                {
                    { "Number", (string)addrEl.Element("Number") },
                    { "Street", (string)addrEl.Element("Street") },
                    { "City",   (string)addrEl.Element("City")   },
                    { "State",  (string)addrEl.Element("State")  },
                    { "Zip",    (string)addrEl.Element("Zip")    },
                    { "NearestAirport", (string)addrEl.Attribute("NearestAirport") }
                };

                if (address.Values.Any(v => string.IsNullOrWhiteSpace(v)))
                    throw new InvalidDataException("Address is missing a required field or NearestAirport attribute.");

                var hotelObj = new Dictionary<string, object>
                {
                    ["Name"] = name,
                    ["Phone"] = phones,
                    ["Address"] = address
                };

                // Optional Rating attribute -> "_Rating"
                var rating = (string)h.Attribute("Rating");
                if (!string.IsNullOrWhiteSpace(rating))
                    hotelObj["_Rating"] = rating;

                hotelsArray.Add(hotelObj);
            }

            var root = new Dictionary<string, object>
            {
                ["Hotels"] = new Dictionary<string, object>
                {
                    ["Hotel"] = hotelsArray
                }
            };

            string jsonText = JsonConvert.SerializeObject(root, Formatting.Indented);
            return jsonText;
        }
    }
}
