using EmailSenderAPI.Models;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Web.Http.Results;

namespace EmailSenderAPI.Controllers
{
    public class EmailController : ApiController
    {       
        [HttpPost]
        [Route("api/email/send", Name = "email-send")]
        public IHttpActionResult Send(EmailObject emailObject)
        {
            if (emailObject == null)
            {
                return BadRequest("Form is empty");
            }

            if (emailObject.Recipient == null)
            {
                return BadRequest("'To' field can not be empty");
            }

            var recipients = emailObject.Recipient.Split(',');

            foreach (var email in recipients)
            {
                if (!IsEmail(email))
                {
                    return BadRequest(string.Format("Invalid email ({0})", email));
                }
            }
            
            var recipientsCC = emailObject.Cc != null ? emailObject.Cc.Split(',') : new string[] { };

            foreach (var email in recipientsCC)
            {
                if (!IsEmail(email))
                {
                    return BadRequest(string.Format("Invalid email ({0})", email));
                }
            }

            var recipientsBCC = emailObject.Bcc != null ? emailObject.Bcc.Split(',') : new string[] { };

            foreach (var email in recipientsBCC)
            {
                if (!IsEmail(email))
                {
                    return BadRequest(string.Format("Invalid email ({0})", email));
                }
            }

            string sendGridResult = SendGridSendMessage(emailObject);
            
            //if send failed
            if (sendGridResult != null)
            {
                //try the other service
                string mailgunResult = MailgunSendMessage(emailObject);

                if (mailgunResult != null)
                {                    
                    //both services failed - return detailed error message tto user
                    return BadRequest(string.Format("Email send failed. (SendGrid:{0}, MailGun:{1})", sendGridResult, mailgunResult));
                }
            }

            return Ok();
        }

        [HttpGet]
        [Route("api/email/test", Name = "email-test")]
        public IHttpActionResult Test()
        {
            var testResults = new List<string>();

            //test for bad recipient address
            var email = new EmailObject();
            email.Subject = "Email test";
            email.Message = "Some email test message";
            var result = Send(email);
            testResults.Add("Bad_Recipient: " + (result is BadRequestErrorMessageResult ? "PASSED" : "FAILED"));

            //invalid email in 'to' field            
            email.Recipient = "test@";
            result = Send(email);
            testResults.Add("Invalid_To_Email: " + (result is BadRequestErrorMessageResult ? "PASSED" : "FAILED"));

            //invalid email in 'cc' field           
            email.Recipient = "test@test.com";
            email.Cc = "test@";
            result = Send(email);
            testResults.Add("Invalid_CC_Email: " + (result is BadRequestErrorMessageResult ? "PASSED" : "FAILED"));

            //invalid email in 'bcc' field (multiple emails)          
            email.Recipient = "test@test.com";
            email.Cc = null;
            email.Bcc = "test@test.com,aaaaa@";
            result = Send(email);
            testResults.Add("Invalid_BCC_Email: " + (result is BadRequestErrorMessageResult ? "PASSED" : "FAILED"));

            //invalid delimiter in 'bcc' field            
            email.Recipient = "aaa@test.com";
            email.Cc = null;
            email.Bcc = "bb@test.com;bbb@test.com";
            result = Send(email);
            testResults.Add("Invalid_BCC_Email_Delimiter: " + (result is BadRequestErrorMessageResult ? "PASSED" : "FAILED"));
            
            //test email sending with sendgrid  
            email.Recipient = "noone@bk.ru";
            email.Cc = "alex.kharkov@catchoftheday.com.au";
            email.Bcc = "alexey.kharkov@gmail.com";
            var emailSendResult = SendGridSendMessage(email);
            testResults.Add("Send_Sendgrid_Email: " + (emailSendResult == null ? "PASSED" : string.Format("FAILED ({0})", emailSendResult)));

            //test email sending with mailgun            
            emailSendResult = MailgunSendMessage(email);
            testResults.Add("Send_Mailgun_Email: " + (emailSendResult == null ? "PASSED" : string.Format("FAILED ({0})", emailSendResult)));

            //full cyclce test
            result = Send(email);
            testResults.Add("Full_Cycle_Test: " + (result is OkResult ? "PASSED" : "FAILED"));
            
            return Ok(testResults);
        }

        protected bool IsEmail(string email)
        {
            return Regex.IsMatch(email, @"\A(?:[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)\Z");
        }

        protected string MailgunSendMessage(EmailObject email)
        {
            //I dont have a domain to setup with mailgun and default mailgun subdomain only allows to send to a list of verified recipients
            //if you wish to test this emailer with your emails, please send me the list of emails to authorize or use your own mailgun api account
            RestClient client = new RestClient();
            client.BaseUrl = new Uri("https://api.mailgun.net/v3");
            client.Authenticator = new HttpBasicAuthenticator("api", "key-0879dc46b14bacf5b06008503e78c6a8");
            RestRequest request = new RestRequest();
            request.AddParameter("domain", "sandboxb37ec2a34fe3470bbb26365312b14fe4.mailgun.org", ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", "Mailgun Sandbox <postmaster@sandboxb37ec2a34fe3470bbb26365312b14fe4.mailgun.org>");
            request.AddParameter("to", email.Recipient);
            request.AddParameter("cc", email.Cc);
            request.AddParameter("bcc", email.Bcc);
            request.AddParameter("subject", email.Subject);
            request.AddParameter("text", email.Message);
            request.Method = Method.POST;
            var result = client.Execute(request);
            return result.IsSuccessful ? null : result.Content;     
        }

        protected string SendGridSendMessage(EmailObject email)
        {
            try
            {
                //sendgrid api requires installation of their client libarray which is forbidden by the task requirements
                //opted to using thier other method - SMTP relay
                MailMessage mail = new MailMessage("you@yourcompany.com", email.Recipient);

                if (!string.IsNullOrEmpty(email.Cc))
                {
                    mail.CC.Add(email.Cc);
                }

                if (!string.IsNullOrEmpty(email.Bcc))
                {
                    mail.Bcc.Add(email.Bcc);
                }

                SmtpClient client = new SmtpClient();
                client.Port = 25;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.Host = "smtp.sendgrid.net";
                client.Credentials = new System.Net.NetworkCredential("apikey", "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");
                mail.Subject = email.Subject;
                mail.Body = email.Message;
                client.Send(mail);
            }
            catch (Exception ex)
            {                
                return ex.Message;
            }

            return null;           
        }
    }
}
