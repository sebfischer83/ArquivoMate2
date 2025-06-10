namespace ArquivoMate2.Infrastructure.Services.Llm
{
    public class OpenAIHelper
    {
        public static string SchemaJson = """
    {
        "type": "object",
        "properties": {
            "date": { "type": "string" },
            "documentType": { "type": "string" },
            "sender": {
                "type": "object",
                "properties": {
                    "firstName": { "type": "string" },
                    "lastName": { "type": "string" },
                    "companyName": { "type": "string" },
                    "street": { "type": "string" },
                    "houseNumber": { "type": "string" },
                    "postalCode": { "type": "string" },
                    "city": { "type": "string" }
                },            "required": ["firstName","lastName","companyName","street","houseNumber","postalCode","city"],
                "additionalProperties": false
            },
            "recipient": {
                "type": "object",
                "properties": {
                    "firstName": { "type": "string" },
                    "lastName": { "type": "string" },
                    "companyName": { "type": "string" },
                    "street": { "type": "string" },
                    "houseNumber": { "type": "string" },
                    "postalCode": { "type": "string" },
                    "city": { "type": "string" }
                },
                "required": ["firstName","lastName","companyName","street","houseNumber","postalCode","city"],
                "additionalProperties": false
            },
            "customerNumber": { "type": "string" },
            "invoiceNumber": { "type": "string" },
            "totalPrice": { "type": "number" },
            "title": { "type": "string" },
            "keywords": {
                "type": "array",
                "items": { "type": "string" }
            },
            "summary": { "type": "string" }
        },
        "required": ["date","documentType","sender","recipient","customerNumber","invoiceNumber","totalPrice","title","keywords","summary"],
        "additionalProperties": false
    }
    """;
    }
}
