using DevTools.Logging.Abstractions;
using DevTools.Logging.Targets;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Extensions;

public static class LoggingBuilderExtensions
{
    extension(ILoggingBuilder builder)
    {
        /// <summary>
        /// Adds file logging via <see cref="FileLogProcessor"/>.
        /// Runtime <c>Enable&lt;FileLoggingOptions&gt;(options)</c> activates the sink.
        /// </summary>
        public ILoggingBuilder AddFileLogging()
        {
            builder.Services.TryAddSingleton<IFileLogTarget, FileLogProcessor>();
            return builder;
        }
        /// <summary>
        /// Adds HTTP logging via <see cref="HttpLogProcessor"/>.
        /// Runtime <c>Enable&lt;HttpLoggingOptions&gt;(options)</c> activates the sink.
        /// </summary>
        public ILoggingBuilder AddHttpLogging()
        {
            builder.Services.TryAddSingleton<IHttpLogTarget, HttpLogProcessor>();
            return builder;
        }
    }
}
