

We got major enhancement, We need to update code to support Elastic Server instead of Postgress database. But dont remove postgress code, add if else conditions accordingly. Based on db type configuration in appsettings, you need to connect to that server


Can you create new excel with these columns 
DocumentId - guid
parentGlobalExporterId - int
parentGlobalImporterId - int
productDesc - string
productDescription - string
productDescEnglish - string
countryId - int
unitPrice - double

In this excel, create 10k rows of sample data with below options. for now, add same text in productDesc, productDescription, productDescEnglish
parentGlobalExporterId - 1, 3, 5, 7, 9
parentGlobalImporterId - 2, 4, 6, 8, 10, 12, 14
productDesc - Lemon Soda, Blueberry Soda, Mehandi, Green Tea, Red Label Tea, Coffee
countryIdMapping:
    1 - India
    2 - Australia
    3 - USA
    4 - UK
    5 - France
    6 - Germany


Can you create one more api in controller to read this excel and insert into elasticserver

for this request
{
  "query": "Load Top 5 documents for Mehandi?"
}
Its generating below elastic query, 
"{\"size\": 5, \"query\": {\"bool\": {\"should\": [{\"match\": {\"productDesc\": \"Mehandi\"}}, {\"match\": {\"productDescEnglish\": \"Mehandi\"}}, {\"match\": {\"productDescription\": \"Mehandi\"}}]}}}"
giving below error 
Request failed to execute. Call: Status code 400 from: POST /_search?typed_keys=true. ServerError: Type: parsing_exception Reason: "Unknown key for a VALUE_STRING in [query]."