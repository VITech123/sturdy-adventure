using System;
using System.IO;
using System.Text.RegularExpressions;
using Tesseract;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using NPOI.XWPF.UserModel;
using EnterpriseWorkReport.Models;
using Docnet.Core;
using Docnet.Core.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EnterpriseWorkReport.Services
{
    public static class OcrService
    {
        private static readonly string[] ResumeKeywords = { "RESUME", "CURRICULUM VITAE", "CV", "PROFILE", "CANDIDATE" };

        public static string GenerateThumbnail(string pdfPath)
        {
            try
            {
                string ext = Path.GetExtension(pdfPath).ToLower();
                if (ext != ".pdf") return null;

                string thumbDir = Path.Combine(DatabaseService.AppDataFolder, "thumbnails");
                if (!Directory.Exists(thumbDir)) Directory.CreateDirectory(thumbDir);

                string thumbPath = Path.Combine(thumbDir, Guid.NewGuid().ToString() + ".png");

                using (var library = DocLib.Instance)
                using (var docReader = library.GetDocReader(pdfPath, new PageDimensions(1080, 1920)))
                {
                    using (var pageReader = docReader.GetPageReader(0))
                    {
                        var rawBytes = pageReader.GetImage();
                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();

                        using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                        {
                            var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
                            Marshal.Copy(rawBytes, 0, bmpData.Scan0, rawBytes.Length);
                            bmp.UnlockBits(bmpData);
                            bmp.Save(thumbPath, System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                return thumbPath;
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Thumbnail error: {ex.Message}");
                return null; 
            }
        }

        public static Resume ExtractDetailsFromResume(string imagePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(imagePath);
            // Replace common separators with spaces for cleaner names
            string candidateName = fileName.Replace(".", " ").Replace("_", " ").Replace("-", " ");

            var resume = new Resume 
            { 
                CandidateName = candidateName,
                ImagePath = imagePath, 
                Status = "Pending", 
                UploadDate = DateTime.Now 
            };

            try
            {
                string ext = Path.GetExtension(imagePath).ToLower();
                resume.ThumbnailPath = (ext == ".pdf") ? GenerateThumbnail(imagePath) : (IsImage(ext) ? imagePath : null);
            }
            catch { }

            return resume;
        }

        public static bool IsImage(string ext)
        {
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff";
        }

        private static string ExtractTextFromPdf(string path)
        {
            var text = new System.Text.StringBuilder();
            try
            {
                using (var pdfReader = new PdfReader(path))
                using (var pdfDoc = new PdfDocument(pdfReader))
                {
                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var strategy = new LocationTextExtractionStrategy();
                        var pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                        text.AppendLine(pageText);
                    }
                }
            }
            catch (Exception ex) { return $"[PDF Error: {ex.Message}]"; }
            return text.ToString();
        }

        private static string ExtractTextFromDocx(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var doc = new XWPFDocument(fs);
                    var text = new System.Text.StringBuilder();
                    foreach (var para in doc.Paragraphs)
                    {
                        text.AppendLine(para.Text);
                    }
                    return text.ToString();
                }
            }
            catch (Exception ex) { return $"[DOCX Error: {ex.Message}]"; }
        }

        private static string ExtractEmail(string text)
        {
            var match = Regex.Match(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.IgnoreCase);
            return match.Success ? match.Value : "";
        }

        private static string ExtractPhone(string text)
        {
            // Simple regex for Indian/International phone numbers
            var match = Regex.Match(text, @"(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}");
            return match.Success ? match.Value : "";
        }

        private static string ExtractExperience(string text)
        {
            // Look for "X years", "X+ years", "X.Y years"
            var match = Regex.Match(text, @"(\d+(\.\d+)?)\s*\+?\s*(years?|yrs?)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value + " Years" : "";
        }

        private static string ExtractName(string text)
        {
            // 1. Try to find "Name: [Value]" pattern
            var nameMatch = Regex.Match(text, @"(?:Name|Candidate Name|Full Name)\s*[:\-]\s*([A-Za-z\s\.\u0080-\uFFFF]+)", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                string name = nameMatch.Groups[1].Value.Trim();
                if (name.Length > 2 && name.Length < 50) return name;
            }

            // 2. Fallback to heuristic
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                string cleanLine = line.Trim();
                
                // Specifically filter out company names and headers
                if (cleanLine.IndexOf("Vendhan", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cleanLine.IndexOf("InfoTech", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (cleanLine.IndexOf("Trust in Every Byte", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                
                if (cleanLine.Length < 3 || cleanLine.Length > 50) continue;
                
                bool isKeyword = false;
                foreach (var kw in ResumeKeywords)
                {
                    if (cleanLine.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) { isKeyword = true; break; }
                }
                if (isKeyword) continue;

                // If it contains symbols like @ or http, it's not a name
                if (cleanLine.Contains("@") || cleanLine.Contains("http") || cleanLine.Contains("www") || cleanLine.Contains("Cell")) continue;
                
                // If it contains too many numbers, it's likely an address or date
                int digitCount = 0;
                foreach (char c in cleanLine) if (char.IsDigit(c)) digitCount++;
                if (digitCount > 2) continue;

                // Check if it looks like a name (at least two words)
                var words = cleanLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 1) return cleanLine;
            }
            return "";
        }
    }
}
