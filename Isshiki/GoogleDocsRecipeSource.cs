using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;

namespace Isshiki
{
    public class GoogleDocsRecipeSource : IDisposable
    {
        private const string ApplicationName = "Isshiki";
        private const string RecipesDocumentId = "1wuf_9JDuW3kKBTdPgoISmZspB4CTF8Om7qagRCzQNxc";
        private readonly DocsService docsService;
        
        public GoogleDocsRecipeSource(string jsonCredential)
        {
            var credential = GoogleCredential
                .FromJson(jsonCredential)
                .CreateScoped(DocsService.ScopeConstants.DocumentsReadonly)
                .UnderlyingCredential as ServiceAccountCredential;
            docsService = new DocsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });
        }

        public async Task<List<Recipe>> GetRecipesAsync()
        {
            var request = docsService.Documents.Get(RecipesDocumentId);
            var doc = await request.ExecuteAsync();

            var result = new List<Recipe>();
            Recipe currentRecipe = null;
            foreach (var se in doc.Body.Content)
            {
                if (se.Paragraph == null)
                {
                    continue;
                }
                var p = se.Paragraph;

                var isNewRecipeStart = IsHeader2(p);
                if (isNewRecipeStart)
                {
                    if (currentRecipe != null)
                    {
                        FinalizeRecipe(currentRecipe);
                        result.Add(currentRecipe);
                    }

                    currentRecipe = new Recipe
                    {
                        Name = p.Elements.FirstOrDefault()?.TextRun?.Content?.Trim()
                    };
                }
                else
                {
                    AddToCurrentRecipe(currentRecipe, p);
                }
            }

            if (currentRecipe != null)
            {
                FinalizeRecipe(currentRecipe);
                result.Add(currentRecipe);
            }

            return result;
        }

        private void FinalizeRecipe(Recipe currentRecipe)
        {
            currentRecipe.Text = currentRecipe.Text.Trim();
            ComputeTags(currentRecipe);
        }

        private void ComputeTags(Recipe recipe)
        {
            var regex = new Regex(@"#\w+");
            recipe.Tags = regex
                .Matches(recipe.Text)
                .Select(m => m.Value)
                .Distinct()
                .ToArray();
        }

        private void AddToCurrentRecipe(Recipe currentRecipe, Paragraph paragraph)
        {
            var sb = new StringBuilder();
            foreach (var element in paragraph.Elements)
            {
                if (element.TextRun == null)
                {
                    continue;
                }
                sb.Append(element.TextRun.Content);
            }

            currentRecipe.Text += sb.ToString();
        }

        private bool IsHeader2(Paragraph paragraph)
        {
            return paragraph.ParagraphStyle.NamedStyleType == "HEADING_2";
        }

        public void Dispose()
        {
            docsService.Dispose();
        }
    }
}