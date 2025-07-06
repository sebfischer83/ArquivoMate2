using AutoMapper;
using Microsoft.Extensions.Localization;
using System;

namespace ArquivoMate2.Infrastructure.Mapping
{
    /// <summary>
    /// Marker class for localization resources
    /// </summary>
    public class SharedResources
    {
    }

    /// <summary>
    /// AutoMapper resolver for translating enum values using IStringLocalizer.
    /// </summary>
    /// <typeparam name="TSource">Source object type</typeparam>
    /// <typeparam name="TDestination">Destination object type</typeparam>
    /// <typeparam name="TEnum">Enum type to translate</typeparam>
    public class EnumTranslationResolver<TSource, TDestination, TEnum> : IValueResolver<TSource, TDestination, string>
        where TEnum : struct, Enum
    {
        private readonly IStringLocalizer<SharedResources> _localizer;

        public EnumTranslationResolver(IStringLocalizer<SharedResources> localizer)
        {
            _localizer = localizer;
        }

        public string Resolve(TSource source, TDestination destination, string destMember, ResolutionContext context)
        {
            // Get the enum value from the source using the member name
            var enumValue = GetEnumValue(source, destMember);
            
            if (enumValue == null)
                return string.Empty;

            // Create resource key: EnumTypeName_EnumValueName
            var resourceKey = $"{typeof(TEnum).Name}_{enumValue}";
            
            // Try to get localized string
            var localizedString = _localizer[resourceKey];
            
            // If localization is not found, fall back to enum ToString()
            return localizedString.ResourceNotFound ? enumValue.ToString() : localizedString.Value;
        }

        private TEnum? GetEnumValue(TSource source, string memberName)
        {
            if (source == null) return null;

            // Use reflection to get the enum property value
            var property = typeof(TSource).GetProperty(memberName.Replace("Status", "").Replace("Source", ""));
            if (property == null)
            {
                // Try to find by exact member name
                foreach (var prop in typeof(TSource).GetProperties())
                {
                    if (prop.PropertyType == typeof(TEnum) || prop.PropertyType == typeof(TEnum?))
                    {
                        var value = prop.GetValue(source);
                        if (value != null)
                            return (TEnum)value;
                    }
                }
                return null;
            }

            var enumValue = property.GetValue(source);
            return enumValue as TEnum?;
        }
    }

    /// <summary>
    /// Specific resolver for DocumentProcessingStatus/ProcessingStatus enums
    /// </summary>
    public class StatusTranslationResolver<TSource, TDestination> : IValueResolver<TSource, TDestination, string>
    {
        private readonly IStringLocalizer<SharedResources> _localizer;

        public StatusTranslationResolver(IStringLocalizer<SharedResources> localizer)
        {
            _localizer = localizer;
        }

        public string Resolve(TSource source, TDestination destination, string destMember, ResolutionContext context)
        {
            if (source == null) return string.Empty;

            // Find the Status property
            var statusProperty = typeof(TSource).GetProperty("Status");
            if (statusProperty == null) return string.Empty;

            var statusValue = statusProperty.GetValue(source);
            if (statusValue == null) return string.Empty;

            // Create resource key for status
            var resourceKey = $"ProcessingStatus_{statusValue}";
            
            // Try to get localized string
            var localizedString = _localizer[resourceKey];
            
            // If localization is not found, fall back to enum ToString()
            return localizedString.ResourceNotFound ? statusValue.ToString() : localizedString.Value;
        }
    }

    /// <summary>
    /// Specific resolver for ImportSource enums
    /// </summary>
    public class ImportSourceTranslationResolver<TSource, TDestination> : IValueResolver<TSource, TDestination, string>
    {
        private readonly IStringLocalizer<SharedResources> _localizer;

        public ImportSourceTranslationResolver(IStringLocalizer<SharedResources> localizer)
        {
            _localizer = localizer;
        }

        public string Resolve(TSource source, TDestination destination, string destMember, ResolutionContext context)
        {
            if (source == null) return string.Empty;

            // Find the Source property
            var sourceProperty = typeof(TSource).GetProperty("Source");
            if (sourceProperty == null) return string.Empty;

            var sourceValue = sourceProperty.GetValue(source);
            if (sourceValue == null) return string.Empty;

            // Create resource key for import source
            var resourceKey = $"ImportSource_{sourceValue}";
            
            // Try to get localized string
            var localizedString = _localizer[resourceKey];
            
            // If localization is not found, fall back to enum ToString()
            return localizedString.ResourceNotFound ? sourceValue.ToString() : localizedString.Value;
        }
    }
}