namespace CorsProxy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configurando o CORS para permitir qualquer origem, m�todo e cabe�alho
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseCors();

            var summaries = new[]
            {
                    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
                };

            app.MapGet("/", async (HttpContext httpContext) =>
            {
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                // Obt�m a URL de destino como um par�metro de query chamado 'url'
                var targetUrl = httpContext.Request.Query["url"].FirstOrDefault();

                if (string.IsNullOrEmpty(targetUrl))
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("O par�metro 'url' � obrigat�rio.");
                    return Results.BadRequest();
                }

                // Constr�i a URL completa de destino (mant�m o path e a query original)
                var queryString = string.Join("&", httpContext.Request.Query
                    .Where(q => q.Key != "url")
                    .Select(q => $"{q.Key}={q.Value}"));
                var fullTargetUrl = $"{targetUrl}{httpContext.Request.Path}{(string.IsNullOrEmpty(queryString) ? "" : $"?{queryString}")}";

                using var httpClient = new HttpClient();

                // Copia o m�todo HTTP da requisi��o original
                var method = new HttpMethod(httpContext.Request.Method);
                var request = new HttpRequestMessage(method, fullTargetUrl);

                // Copia os cabe�alhos da requisi��o original para a nova requisi��o
                foreach (var header in httpContext.Request.Headers)
                {
                    if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) &&
                        !header.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase)) // Evita conflitos
                    {
                        foreach (var value in header.Value)
                        {
                            request.Headers.TryAddWithoutValidation(header.Key, value);
                        }
                    }
                }
                // Copia o corpo da requisi��o original (se houver)
                if (httpContext.Request.Body != null && httpContext.Request.ContentLength > 0)
                {
                    var content = new StreamContent(httpContext.Request.Body);
                    if (!string.IsNullOrEmpty(httpContext.Request.ContentType))
                    {
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(httpContext.Request.ContentType);
                    }
                    request.Content = content;
                }
                try
                {
                    var response = await httpClient.SendAsync(request);

                    // Copia o status code da resposta da API de destino
                    httpContext.Response.StatusCode = (int)response.StatusCode;

                    // Copia os cabe�alhos da resposta da API de destino
                    //foreach (var header in response.Headers)
                    //{
                    //    if (!header.Key.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                    //    {
                    //        httpContext.Response.Headers[header.Key] = header.Value.ToArray();
                    //    }
                    //}

                    foreach (var header in response.Content.Headers)
                    {
                        if (!header.Key.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                        {
                            httpContext.Response.Headers[header.Key] = header.Value.ToArray();
                        }
                    }

                    // Copia o corpo da resposta da API de destino
                    // Obt�m o stream do conte�do da resposta e copia para o response body
                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        await responseStream.CopyToAsync(httpContext.Response.Body);
                    }

                    startTime.Stop();
                    Console.WriteLine($"Request to {fullTargetUrl} took {startTime.ElapsedMilliseconds} ms");

                    return Results.Empty; // Adicionado o retorno de Results.Empty
                }
                catch (Exception ex)
                {
                    httpContext.Response.StatusCode = 500;
                    await httpContext.Response.WriteAsync($"Erro ao acessar a API de destino: {ex.Message}");
                    return Results.StatusCode(500); // Retorna um StatusCodeResult em caso de erro

                }
            });

            app.MapGet("/weatherforecast", (HttpContext httpContext) =>
            {
                var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = summaries[Random.Shared.Next(summaries.Length)]
                    })
                    .ToArray();
                return forecast;
            });

            app.Run();
        }
    }
}
