using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using System.Text;
using System;
using System.Text.Json;

namespace Summlyzer.Controllers
{
    [ApiController]
    [Route("api/[action]")]
    public class SummarizeController : ControllerBase
    {
        [HttpPost]

        public async Task<IActionResult> SummarizeText([FromForm] string inputText, [FromForm] int summaryLength)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return BadRequest("Input text cannot be empty.");

            int wordCount = inputText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 750)
                return BadRequest($"Word limit exceeded: {wordCount} words. Max allowed is 750.");

            Console.WriteLine($"Text received with {wordCount} words. Summary length: {summaryLength}");

            string summarized = await CallHuggingFaceSummarizer(inputText, summaryLength);
            List<string> keywords = await CallHuggingFaceClassifier(inputText);

            return Ok(new
            {
                inputText,
                summarizedText = summarized,
                keywords
            });
        }


        [HttpPost]
        public async Task<IActionResult> SummarizeFile(IFormFile file, [FromForm] int summaryLength)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            Console.WriteLine($"File received: {file.FileName}");
            Console.WriteLine($"Summary length: {summaryLength}");

            string extension = Path.GetExtension(file.FileName).ToLower();
            string text = extension switch
            {
                ".pdf" => await ReadPdfFile(file),
                ".docx" => await ReadDocxFile(file),
                ".txt" => await ReadTxtFile(file),
                _ => null
            };

            if (text == null)
                return BadRequest("Unsupported file format. Only .pdf, .docx, and .txt are allowed.");

            Console.WriteLine($"File content:\n{text}");

            int wordCount = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 750)
                return BadRequest($"Word limit exceeded: {wordCount} words. Max allowed is 750.");

            System.Console.WriteLine(text);


            string summarized = await CallHuggingFaceSummarizer(text, summaryLength);

            List<string> keywords = await CallHuggingFaceClassifier(text);

            return Ok(new
            {
                inputText = text,
                summarizedText = summarized,
                keywords = keywords
            });

        }



        private async Task<string> ReadTxtFile(IFormFile file)
        {
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync();
        }

        private async Task<string> ReadDocxFile(IFormFile file)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var doc = WordprocessingDocument.Open(stream, false);

            var sb = new StringBuilder();
            var body = doc.MainDocumentPart?.Document?.Body;

            if (body != null)
                sb.Append(body.InnerText);

            return sb.ToString();
        }

        private async Task<string> ReadPdfFile(IFormFile file)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            stream.Position = 0;
            var sb = new StringBuilder();

            using var pdf = PdfDocument.Open(stream);
            foreach (var page in pdf.GetPages())
            {
                foreach (var word in page.GetWords()) // Ensures proper spacing
                {
                    sb.Append(word.Text + " ");
                }
            }

            return sb.ToString().Trim();
        }
        private async Task<string> CallHuggingFaceSummarizer(string inputText, int summaryLength)
        {
            using var httpClient = new HttpClient();
            int min_length;

            var apiUrl = "https://api-inference.huggingface.co/models/tuner007/pegasus_summarizer";

            if (summaryLength == 1)
            {
                apiUrl = "https://api-inference.huggingface.co/models/facebook/bart-large-xsum";
                min_length = 16;
            }
            else if (summaryLength == 2)
            {
                apiUrl = "https://api-inference.huggingface.co/models/tuner007/pegasus_summarizer";
                min_length = 48;
            }
            else
            {
                apiUrl = "https://api-inference.huggingface.co/models/utrobinmv/t5_summary_en_ru_zh_base_2048";
                min_length = 192;
            }

            // Replace with your actual Hugging Face token
            var apiToken = "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"; // <-- EDIT THIS VALUE
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

            var payload = new
            {
                inputs = inputText,
                parameters = new
                {
                    min_length = min_length,
                    max_length = 1024, // You can make this dynamic
                    do_sample = false
                }
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"HuggingFace API Error: {error}");
                return "[Error generating summary]";
            }

            var resultJson = await response.Content.ReadAsStringAsync();

            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var summary = doc.RootElement[0].GetProperty("summary_text").GetString();
                return summary ?? "[No summary returned]";
            }
            else
            {
                Console.WriteLine("Unexpected response format.");
                return "[Unexpected response format]";
            }
        }

        private async Task<List<string>> CallHuggingFaceClassifier(string inputText)
        {
            using var httpClient = new HttpClient();

            const string apiUrl = "https://api-inference.huggingface.co/models/facebook/bart-large-mnli";
            var apiToken = "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"; // <-- Replace if needed
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

            var candidateLabels = new[]
            {
        "technology", "finance", "health", "education", "sports",
        "politics", "entertainment", "environment", "travel", "science",
        "culture", "business", "real estate", "law", "media",
        "transportation", "agriculture", "energy", "fashion", "food"
    };

            var payload = new
            {
                inputs = inputText,
                parameters = new
                {
                    candidate_labels = candidateLabels,
                    multi_label = true,
                    hypothesis_template = "This text is about {}."
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"HuggingFace Classifier API Error: {error}");
                return new List<string> { "[Classification failed]" };
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resultJson);

            if (doc.RootElement.TryGetProperty("labels", out var labelsElem) &&
                doc.RootElement.TryGetProperty("scores", out var scoresElem))
            {
                var labels = labelsElem.EnumerateArray().Select(x => x.GetString()).ToList();
                var scores = scoresElem.EnumerateArray().Select(x => x.GetDouble()).ToList();

                var top5 = labels.Zip(scores, (label, score) => new { label, score })
                                 .OrderByDescending(x => x.score)
                                 .Take(5)
                                 .Select(x => x.label ?? "")
                                 .ToList();

                return top5;
            }

            Console.WriteLine("Unexpected classifier response format.");
            return new List<string> { "[No labels returned]" };
        }


    }
}
