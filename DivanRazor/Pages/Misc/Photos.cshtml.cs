using DivanRazor.Models;
using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.Divan.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DivanRazor.Pages
{
    public class PhotosModel : LoginPartialEnabledPageModel
    {
        public string LastError { get; set; }

        public List<DivanPoetViewModel> Poets { get; set; }

        public DivanPoetViewModel Poet { get; set; }

        public List<DivanPoetSuggestedSpecLineViewModel> SpecLines { get; set; }

        public List<DivanPoetSuggestedPictureViewModel> Photos { get; set; }

        [BindProperty]
        public PoetPhotoSuggestionUploadModel Upload { get; set; }

        public DivanPoetSuggestedPictureViewModel UploadedPhoto { get; set; }

        public bool ModeratePoetPhotos { get; set; }

        // Kept local rather than PoetCacheService: unlike every other page that fetches the poet
        // list, this one prefixes each poet's ImageUrl with APIRoot.InternetUrl before use, which
        // the shared service intentionally doesn't do (no other caller needed it).
        private async Task<List<DivanPoetViewModel>> _PreparePoets()
        {
            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poets");
            if (!response.IsSuccessStatusCode)
            {
                LastError = await ReadErrorMessageAsync(response);
                return new List<DivanPoetViewModel>();
            }
            var poets = JArray.Parse(await response.Content.ReadAsStringAsync()).ToObject<List<DivanPoetViewModel>>();

            foreach (var poet in poets)
            {
                poet.ImageUrl = $"{APIRoot.InternetUrl}{poet.ImageUrl}";
            }

            return poets;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            var maintenanceResult = TryGetMaintenanceModeResult();
            if (maintenanceResult != null)
            {
                return maintenanceResult;
            }

            InitializeCommonPageState();

            if (!string.IsNullOrEmpty(Request.Query["p"]))
            {
                var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poet?url=/{Request.Query["p"]}");
                if (!response.IsSuccessStatusCode)
                {
                    LastError = await ReadErrorMessageAsync(response);
                    return Page();
                }
                Poet = JObject.Parse(await response.Content.ReadAsStringAsync()).ToObject<DivanPoetCompleteViewModel>().Poet;
                Poet.ImageUrl = $"{APIRoot.InternetUrl}{Poet.ImageUrl}";

                var responseLines = await _httpClient.GetAsync($"{APIRoot.Url}/api/poetspecs/poet/{Poet.Id}");
                if (!responseLines.IsSuccessStatusCode)
                {
                    LastError = await ReadErrorMessageAsync(responseLines);
                    return Page();
                }
                SpecLines = JArray.Parse(await responseLines.Content.ReadAsStringAsync()).ToObject<List<DivanPoetSuggestedSpecLineViewModel>>();

                var responsePhotos = await _httpClient.GetAsync($"{APIRoot.Url}/api/poetphotos/poet/{Poet.Id}");
                if (!responsePhotos.IsSuccessStatusCode)
                {
                    LastError = await ReadErrorMessageAsync(responsePhotos);
                    return Page();
                }
                Photos = JArray.Parse(await responsePhotos.Content.ReadAsStringAsync()).ToObject<List<DivanPoetSuggestedPictureViewModel>>();

                foreach (var photo in Photos)
                {
                    photo.ImageUrl = $"{APIRoot.InternetUrl}/{photo.ImageUrl}";
                }

                if (LoggedIn)
                {
                    await DivanSessionChecker.ApplyPermissionsToViewData(Request, Response, ViewData);
                    ModeratePoetPhotos = ViewData.ContainsKey($"{RMuseumSecurableItem.DivanEntityShortName}-{RMuseumSecurableItem.ModeratePoetPhotos}");
                }

            }
            else
            {
                Poets = await _PreparePoets();
            }

            ViewData["Title"] = Poet == null ? "پیشنهاد تصویر برای سخنوران" : $"پیشنهاد تصویر برای {Poet.Nickname}";

            return Page();
        }

        private IActionResult SpecLineErrorPartial(string error, string remainingText = null)
        {
            return Partial("_PoetSpecLinePartial", new _PoetSpecLinePartialModel()
            {
                Line = new DivanPoetSuggestedSpecLineViewModel()
                {
                    Id = 0,
                    Contents = error
                },
                SanitizerRemainingText = remainingText
            });
        }

        public Task<IActionResult> OnPostSuggestAsync(int poetId, string contents)
        {
            if (string.IsNullOrEmpty(contents))
            {
                return Task.FromResult(SpecLineErrorPartial("متن خالی است."));
            }

            return WithSecureClientAsync(async secureClient =>
            {
                var response = await secureClient.PostAsync($"{APIRoot.Url}/api/poetspecs",
                    new StringContent(
                    JsonConvert.SerializeObject
                    (
                        new DivanPoetSuggestedSpecLineViewModel()
                        {
                            PoetId = poetId,
                            Contents = contents,
                        }
                    ),
                    Encoding.UTF8, "application/json")
                    );
                if (response.IsSuccessStatusCode)
                {
                    var line = JsonConvert.DeserializeObject<DivanPoetSuggestedSpecLineViewModel>(await response.Content.ReadAsStringAsync());
                    return Partial("_PoetSpecLinePartial", new _PoetSpecLinePartialModel()
                    {
                        Line = line
                    });
                }

                string rawError = await ReadErrorMessageAsync(response);
                var sanitizerInfo = TryParseSanitizerTextDroppedError(rawError);
                return SpecLineErrorPartial(sanitizerInfo != null ? sanitizerInfo.Message : rawError, sanitizerInfo?.RemainingText);
            }, SpecLineErrorPartial(NotLoggedInMessage));
        }

        public async Task<IActionResult> OnPostAsync(PoetPhotoSuggestionUploadModel Upload)
        {
            if (string.IsNullOrEmpty(Upload.Title))
                LastError = "عنوان خالی است.";
            else
               if (string.IsNullOrEmpty(Upload.Description))
                LastError = "توضیح خالی است.";
            else
               if (Upload.Image == null)
                LastError = "تصویر انتخاب نشده است.";
            else
                // Kept as its own using/PrepareClient block rather than WithSecureClientAsync: this
                // handler needs to fall through to OnGetAsync() regardless of outcome (success,
                // upload failure, or auth failure), which doesn't fit the early-return helper shape.
                using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                {
                    if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                    {
                        MultipartFormDataContent form = new MultipartFormDataContent();

                        using (MemoryStream stream = new MemoryStream())
                        {
                            form.Add(new StringContent(Upload.PoetId.ToString()), "poetId");
                            form.Add(new StringContent(Upload.Title), "title");
                            form.Add(new StringContent(Upload.Description), "description");
                            form.Add(new StringContent(""), "srcUrl");


                            await Upload.Image.CopyToAsync(stream);
                            var fileContent = stream.ToArray();
                            form.Add(new ByteArrayContent(fileContent, 0, fileContent.Length), Upload.Image.FileName, Upload.Image.FileName);

                            HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/poetphotos", form);
                            if (!response.IsSuccessStatusCode)
                            {
                                LastError = await ReadErrorMessageAsync(response);
                            }
                            else
                            {
                                UploadedPhoto = JsonConvert.DeserializeObject<DivanPoetSuggestedPictureViewModel>(await response.Content.ReadAsStringAsync());

                                UploadedPhoto.ImageUrl = $"{APIRoot.InternetUrl}/{UploadedPhoto.ImageUrl}";
                            }
                        }
                    }
                    else
                    {
                        LastError = NotLoggedInMessage;
                    }

                }


            return await OnGetAsync();
        }

        public Task<IActionResult> OnPutChoosePhotoAsync(int id)
        {
            return WithSecureClientAsync(async secureClient =>
            {
                var responsePhoto = await secureClient.GetAsync($"{APIRoot.Url}/api/poetphotos/{id}");
                if (!responsePhoto.IsSuccessStatusCode)
                {
                    LastError = await ReadErrorMessageAsync(responsePhoto);
                    return new BadRequestObjectResult(LastError);
                }
                var photo = JsonConvert.DeserializeObject<DivanPoetSuggestedPictureViewModel>(await responsePhoto.Content.ReadAsStringAsync());
                photo.ChosenOne = true;
                var response = await secureClient.PutAsync($"{APIRoot.Url}/api/poetphotos", new StringContent(JsonConvert.SerializeObject(photo), Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    return new BadRequestObjectResult(await ReadErrorMessageAsync(response));
                }
                return new OkResult();
            });
        }

        public Task<IActionResult> OnDeleteAsync(int id)
        {
            return WithSecureClientAsync(async secureClient =>
            {
                var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/poetphotos/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    return new BadRequestObjectResult(await ReadErrorMessageAsync(response));
                }
                return new OkResult();
            });
        }

        public Task<IActionResult> OnDeleteSpecLineAsync(int id)
        {
            return WithSecureClientAsync(async secureClient =>
            {
                var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/poetspecs/{id}");
                if (!response.IsSuccessStatusCode)
                {
                    return new BadRequestObjectResult(await ReadErrorMessageAsync(response));
                }
                return new OkResult();
            });
        }

        public PhotosModel(HttpClient httpClient, IConfiguration configuration) : base(httpClient, configuration)
        {
        }
    }
}
