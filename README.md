"# mailsender" 

Pull, open EmailSenderAPI.sln in Visual Studio, build and run. The initial page will provide an email send form. Send emails or Test the service (goes through a number of test cases).

The direct API addresses are: 

http://localhost:62790/api/email/send/ (POST)
http://localhost:62790/api/email/test/ (GET) - returns test results as an XML

The only NuGet package required for this project is RestSharp.
This project uses JQuery and Bootstrap.