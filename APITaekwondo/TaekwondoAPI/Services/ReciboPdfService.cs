using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;

namespace TaekwondoAPI.Services
{
    public class ReciboPdfService : IReciboPdfService
    {
        private const double PageWidth = 595.5;
        private const double PageHeight = 842.25;
        private readonly IWebHostEnvironment _environment;

        public ReciboPdfService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public byte[] CrearRecibo(ReciboPdfData data)
        {
            var templatePath = Path.Combine(_environment.ContentRootPath, "Assets", "recibo-template.pdf");
            if (File.Exists(templatePath))
            {
                return EstamparPlantilla(File.ReadAllBytes(templatePath), data);
            }

            var content = CrearContenido(data);
            return CrearDocumentoPdf(content);
        }

        private static string CrearContenido(ReciboPdfData data)
        {
            var sb = new StringBuilder();

            sb.AppendLine("q");
            sb.AppendLine("1 w");
            sb.AppendLine("70 690 455 1 re f");
            sb.AppendLine("0.7 w");
            sb.AppendLine("70 180 455 455 re S");
            sb.AppendLine("Q");

            Texto(sb, 137, 735, 30, "RECIBO DE PAGO");
            Texto(sb, 178, 705, 10, "Av. Roque Saenz Pena 444 || 3518 15-4785");

            Texto(sb, 95, 610, 13, "Se registra el recibo de cuota con los siguientes datos:");
            Texto(sb, 115, 555, 12, $"Alumno: {data.Alumno}");
            Texto(sb, 115, 515, 12, $"Mes: {data.Mes}");
            Texto(sb, 115, 475, 12, $"Monto: {data.Monto}");
            Texto(sb, 115, 435, 12, $"Medio de pago: {data.MedioPago}");
            Texto(sb, 115, 395, 12, $"Clase: {data.Clase}");
            Texto(sb, 115, 355, 12, $"Fecha: {data.Fecha:dd/MM/yyyy}");
            Texto(sb, 115, 285, 12, "Gracias.");

            return sb.ToString();
        }

        private static byte[] EstamparPlantilla(byte[] templateBytes, ReciboPdfData data)
        {
            var templateText = Encoding.Latin1.GetString(templateBytes);
            var maxObjectNumber = Regex.Matches(templateText, @"(\d+)\s+0\s+obj")
                .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max();

            var previousXref = ObtenerStartXref(templateText);
            var contentObjectNumber = maxObjectNumber + 1;
            var fontObjectNumber = maxObjectNumber + 2;

            var xObjectReferences = ObtenerXObjectReferences(templateText);
            var content = CrearContenidoEstampado(data);
            var contentBytes = Encoding.Latin1.GetBytes(content);
            var contentObject = CrearStream(contentBytes);
            var fontObject = Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\n");
            var pageObject = Encoding.ASCII.GetBytes($"""
                <<
                /Type /Page
                /Resources <<
                /ProcSet [/PDF /Text /ImageB /ImageC /ImageI]
                /ExtGState <<
                /G3 32 0 R
                /gs2 33 0 R
                >>
                /XObject <<
                {xObjectReferences}
                >>
                /Font <<
                /FRecibo {fontObjectNumber} 0 R
                >>
                >>
                /MediaBox [0.0 7.8299813 595.5 850.07996]
                /Contents [15 0 R {contentObjectNumber} 0 R]
                /StructParents 0
                /Tabs /S
                /Parent 2 0 R
                /BleedBox [0.0 7.8299813 595.5 850.07996]
                /TrimBox [0.0 7.8299813 595.5 850.07996]
                /CropBox [0.0 7.8299813 595.5 850.07996]
                /Rotate 0
                /Annots []
                >>
                """);

            using var pdf = new MemoryStream();
            pdf.Write(templateBytes);
            EscribirAscii(pdf, "\n");

            var pageOffset = pdf.Position;
            EscribirAscii(pdf, "9 0 obj\n");
            pdf.Write(pageObject);
            EscribirAscii(pdf, "endobj\n");

            var contentOffset = pdf.Position;
            EscribirAscii(pdf, $"{contentObjectNumber} 0 obj\n");
            pdf.Write(contentObject);
            EscribirAscii(pdf, "endobj\n");

            var fontOffset = pdf.Position;
            EscribirAscii(pdf, $"{fontObjectNumber} 0 obj\n");
            pdf.Write(fontObject);
            EscribirAscii(pdf, "endobj\n");

            var xrefOffset = pdf.Position;
            EscribirAscii(pdf, "xref\n");
            EscribirAscii(pdf, "9 1\n");
            EscribirAscii(pdf, $"{pageOffset:0000000000} 00000 n \n");
            EscribirAscii(pdf, $"{contentObjectNumber} 2\n");
            EscribirAscii(pdf, $"{contentOffset:0000000000} 00000 n \n");
            EscribirAscii(pdf, $"{fontOffset:0000000000} 00000 n \n");
            EscribirAscii(pdf, $"trailer\n<< /Size {fontObjectNumber + 1} /Root 1 0 R /Prev {previousXref} >>\nstartxref\n{xrefOffset}\n%%EOF\n");

            return pdf.ToArray();
        }

        private static string CrearContenidoEstampado(ReciboPdfData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("q");
            sb.AppendLine("/G3 gs");
            sb.AppendLine("0 0 0 RG");
            sb.AppendLine("0 0 0 rg");
            Texto(sb, 405, 511, 12, data.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            Texto(sb, 188, 362, 12, data.Alumno);
            Texto(sb, 154, 325, 12, data.Monto);
            Texto(sb, 355, 325, 12, data.MedioPago);
            Texto(sb, 188, 287, 12, data.Mes);
            sb.AppendLine("Q");
            return sb.ToString();
        }

        private static string ObtenerXObjectReferences(string templateText)
        {
            var pageMatch = Regex.Match(templateText, @"9\s+0\s+obj\s*<<(?<page>.*?)>>\s*endobj", RegexOptions.Singleline);
            if (!pageMatch.Success)
            {
                return "/X14 14 0 R";
            }

            var xObjectMatch = Regex.Match(pageMatch.Groups["page"].Value, @"/XObject\s*<<(?<xobjects>.*?)>>", RegexOptions.Singleline);
            if (!xObjectMatch.Success)
            {
                return "/X14 14 0 R";
            }

            return xObjectMatch.Groups["xobjects"].Value.Trim();
        }

        private static long ObtenerStartXref(string templateText)
        {
            var match = Regex.Match(templateText, @"startxref\s+(\d+)\s+%%EOF\s*$", RegexOptions.Singleline);
            if (!match.Success)
            {
                throw new InvalidOperationException("No se pudo leer la plantilla PDF del recibo.");
            }

            return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static void Texto(StringBuilder sb, double x, double y, int size, string text)
        {
            sb.AppendLine("BT");
            sb.AppendLine($"/FRecibo {size.ToString(CultureInfo.InvariantCulture)} Tf");
            sb.AppendLine($"{x.ToString(CultureInfo.InvariantCulture)} {y.ToString(CultureInfo.InvariantCulture)} Td");
            sb.Append('(');
            sb.Append(EscaparTexto(text));
            sb.AppendLine(") Tj");
            sb.AppendLine("ET");
        }

        private static string EscaparTexto(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private static byte[] CrearDocumentoPdf(string content)
        {
            var contentBytes = Encoding.Latin1.GetBytes(content);

            var objects = new List<byte[]>
            {
                Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>\n"),
                Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n"),
                Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth.ToString(CultureInfo.InvariantCulture)} {PageHeight.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /FRecibo 5 0 R >> >> /Contents 4 0 R >>\n"),
                CrearStream(contentBytes),
                Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\n")
            };

            using var pdf = new MemoryStream();
            EscribirAscii(pdf, "%PDF-1.4\n");

            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Position);
                EscribirAscii(pdf, $"{i + 1} 0 obj\n");
                pdf.Write(objects[i]);
                EscribirAscii(pdf, "endobj\n");
            }

            var xrefOffset = pdf.Position;
            EscribirAscii(pdf, $"xref\n0 {objects.Count + 1}\n");
            EscribirAscii(pdf, "0000000000 65535 f \n");

            foreach (var offset in offsets.Skip(1))
            {
                EscribirAscii(pdf, $"{offset:0000000000} 00000 n \n");
            }

            EscribirAscii(pdf, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
            return pdf.ToArray();
        }

        private static byte[] CrearStream(byte[] contentBytes)
        {
            using var stream = new MemoryStream();
            EscribirAscii(stream, $"<< /Length {contentBytes.Length} >>\nstream\n");
            stream.Write(contentBytes);
            EscribirAscii(stream, "\nendstream\n");
            return stream.ToArray();
        }

        private static void EscribirAscii(Stream stream, string value)
        {
            stream.Write(Encoding.ASCII.GetBytes(value));
        }
    }
}
