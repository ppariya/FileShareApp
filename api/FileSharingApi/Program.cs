
using FileSharingApi;

namespace FileSharingApi
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			
			// Allow up to 100MB uploads
			builder.WebHost.ConfigureKestrel(options =>
			{
				options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
			});
			builder.Services.AddCors(options =>
			{
				options.AddDefaultPolicy(policy =>
				{
					policy.WithOrigins("http://localhost:3000")
						  .AllowAnyHeader()
						  .AllowAnyMethod();
				});
			});
			builder.Services.AddControllers();
			
			// Add Swagger services
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
				{
					Title = "File Sharing API",
					Version = "v1",
					Description = "A simple API for file upload, download, and management operations"
				});
			});

			var app = builder.Build();

			// Configure Swagger middleware (only in development)
			if (app.Environment.IsDevelopment())
			{
				app.UseSwagger();
				app.UseSwaggerUI(c =>
				{
					c.SwaggerEndpoint("/swagger/v1/swagger.json", "File Sharing API v1");
					c.RoutePrefix = "swagger"; // This makes Swagger UI available at /swagger
				});
			}

			app.UseHttpsRedirection();
			app.UseCors();

			app.MapControllers();

			app.Run();
		}
	}
}
