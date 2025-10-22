using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Features.Processors.LabResults.Domain
{

    public static class LabReportSchemaFactory
    {
        public static string BuildLabReportSchemaJson()
        {
            var schema = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    LabName = new { type = "string" },
                    Patient = new { type = "string" },
                    Values = new
                    {
                        type = "array",
                        description = "Eine Liste von Spalten (z. B. verschiedene Erhebungsdaten).",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                Date = new
                                {
                                    type = "string",
                                    description = "ISO 8601 date for this column",
                                    pattern = @"^\d{4}-\d{2}-\d{2}$"
                                },
                                Label = new
                                {
                                    type = "string",
                                    description = "Originale Spaltenüberschrift/Anzeige (z. B. '30.10.15')"
                                },
                                SourceColumn = new
                                {
                                    type = "string",
                                    description = "Originaler Spaltenname im Dokument (falls abweichend von Label)"
                                },
                                Measurements = new
                                {
                                    type = "array",
                                    description = "Alle Einzelwerte (Parameter) dieser Spalte.",
                                    items = new
                                    {
                                        type = "object",
                                        additionalProperties = false,
                                        properties = new
                                        {
                                            Parameter = new { type = "string" },
                                            Result = new { type = "string" },
                                            Unit = new { type = "string" },
                                            Reference = new { type = "string" }
                                        },
                                        required = new[] { "Parameter", "Result" }
                                    }
                                }
                            },
                            required = new[] { "Date", "Measurements" }
                        }
                    }
                },
                required = new[] { "LabName", "Patient", "Values" }
            };

            var json = JsonSerializer.Serialize(
                schema,
                new JsonSerializerOptions { WriteIndented = true }
            );

            return json;
        }
    }
}
