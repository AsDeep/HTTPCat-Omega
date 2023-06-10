using Microsoft.AspNetCore.Mvc;


namespace Omega_HTTPCat.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CatController : ControllerBase
    {
        private static readonly HttpClient Client = new HttpClient();

        [HttpGet]
        public async Task<IActionResult> GetCatPicture([FromQuery] string url)
        {
            try
            {
                // Проверка формата введенного сайта
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    // Если не приведен к нужному виду, добавляем "https://", предполагая, что сайт поддерживает HTTPS
                    uri = new Uri($"https://{url}");
                }

                var response = await Client.GetAsync(uri);
                var statusCode = (int)response.StatusCode;

                // Проверяем, есть ли картинка с кодом в кэше
                if (!CacheManager.HasCachedPicture(statusCode))
                {
                    // Получаем картинку с http.cat и сохраняем данные
                    var catPictureUrl = $"https://http.cat/images/{statusCode}.jpg";
                    var catPictureResponse = await Client.GetAsync(catPictureUrl);
                    var catPictureBytes = await catPictureResponse.Content.ReadAsByteArrayAsync();

                    // Помещаем картинку в кэш отдельным потоком
                    await Task.Run(() => CacheManager.CachePicture(statusCode, catPictureBytes));
                }

                // Возвращаем в виде ответа картинку из кэша
                var cachedPictureBytes = CacheManager.GetCachedPicture(statusCode);
                return File(cachedPictureBytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                // Возврат ошибки в случае неудачи
                return BadRequest(ex.Message);
            }
        }
    }

    public static class CacheManager
    {
        private static readonly object LockObject = new();
        private static readonly TimeSpan CacheExpirationTime = TimeSpan.FromMinutes(15); 
        private static readonly Dictionary<int, byte[]> CachedPictures = new(); 

        public static bool HasCachedPicture(int statusCode) // Наличие пары "код-котик" в кэше
        {
            lock (LockObject)
            {
                return CachedPictures.ContainsKey(statusCode); 
            }
        }

        public static byte[] GetCachedPicture(int statusCode) 
        {
            lock (LockObject)
            {
                return CachedPictures[statusCode];
            }
        }

        public static void CachePicture(int statusCode, byte[] pictureBytes)
        {
            lock (LockObject)
            {
                CachedPictures[statusCode] = pictureBytes;
                // Храним пару в течение заданного времени
                Task.Delay(CacheExpirationTime).ContinueWith(_ => ClearCache(statusCode));
            }
        }

        private static void ClearCache(int statusCode) 
        {
            lock (LockObject)
            {
                CachedPictures.Remove(statusCode);
            }
        }
    }
    
}
