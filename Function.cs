using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Alexa.NET.Response;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Newtonsoft.Json;
using Alexa.NET;
using System.Net;
using System.Net.Mail;
using KidNationCode.Structs;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace KidNationCode
{
    public class Function
    {

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public SkillResponse FunctionHandler(SkillRequest input, ILambdaContext context)
        {
            ILambdaLogger log = context.Logger;
            log.LogLine($"Skill Request Object:" + JsonConvert.SerializeObject(input));

            Session session = input.Session;
            if (session.Attributes == null)
                session.Attributes = new Dictionary<string, object>();

            Type requestType = input.GetRequestType();
            if (input.GetRequestType() == typeof(LaunchRequest))
            {
                string speech = "Welcome! Say student code you would like to text";
                Reprompt rp = new Reprompt("Say student code to text");
                return ResponseBuilder.Ask(speech, rp, session);
            }
            else if (input.GetRequestType() == typeof(SessionEndedRequest))
            {
                return ResponseBuilder.Tell("Goodbye!");
            }
            else if (input.GetRequestType() == typeof(IntentRequest))
            {
                var intentRequest = (IntentRequest)input.Request;
                switch (intentRequest.Intent.Name)
                {
                    case "AMAZON.CancelIntent":
                    case "AMAZON.StopIntent":
                        return ResponseBuilder.Tell("Goodbye!");
                    case "AMAZON.HelpIntent":
                        {
                            Reprompt rp = new Reprompt("What's the code you would like to text?");
                            return ResponseBuilder.Ask("Say student code you would like to text", rp, session);
                        }
                    case "CodeIntent":
                        {
                            var slots = intentRequest.Intent.Slots;
                            string studentCodeSpoken = $"{slots["callSignA"].Value}{slots["callSignB"].Value}{slots["callSignC"].Value}{slots["callSignD"].Value}";
                            
                            session.Attributes["studentCode"] = studentCodeSpoken.Replace(".","");
                            log.LogLine($"Code received: {session.Attributes["studentCode"]}");
                            string next = $"I heard you say, {session.Attributes["studentCode"]}, is that correct?";
                            Reprompt rp = new Reprompt(next);
                            return ResponseBuilder.Ask(next, rp, session);
                        }
                    case "ConfirmationIntent":
                        {
                            // check answer
                            string confirm = intentRequest.Intent.Slots["YesNo"].Value;
                            var speech = "";
                            if (confirm.ToUpper() != "NO")
                            {
                               var message = SendMail(log, "Parent Requested", session.Attributes["studentCode"].ToString().ToUpper());
                               if (message == string.Empty)
                                   return ResponseBuilder.Tell("Code sent. Goodbye!");
                               else
                                    return ResponseBuilder.Tell($"Sorry, something went wrong the message wasn't sent.  {message} {Environment.GetEnvironmentVariable(EnvironmentVariables.FROM_PASSWORD)}");
                            }
                            else
                            {
                                speech = "Sorry, please say the code again.";                                
                            }
                            Reprompt rp = new Reprompt(speech);
                            return ResponseBuilder.Ask(speech, rp, session);
                        }
                    default:
                        {
                            log.LogLine($"Unknown intent: " + intentRequest.Intent.Name);
                            string speech = "I didn't understand - try again?";
                            Reprompt rp = new Reprompt(speech);
                            return ResponseBuilder.Ask(speech, rp, session);
                        }
                }
            }
            return ResponseBuilder.Tell("Goodbye!");
        }

        private string SendMail(ILambdaLogger log, string subject, string body)
        {            
            log.LogLine($"Sending email : {subject}");
            var fromAddress = new MailAddress(Environment.GetEnvironmentVariable(EnvironmentVariables.FROM_ADDRESS), Environment.GetEnvironmentVariable(EnvironmentVariables.FROM_ADDRESS_NAME));
            var toAddress = new MailAddress(Environment.GetEnvironmentVariable(EnvironmentVariables.TO_ADDRESS), Environment.GetEnvironmentVariable(EnvironmentVariables.TO_ADDRESS_NAME));
            string fromPassword = Environment.GetEnvironmentVariable(EnvironmentVariables.FROM_PASSWORD);            

            var smtp = new SmtpClient
            {
                Host = Environment.GetEnvironmentVariable(EnvironmentVariables.SMTP_HOST),
                Port = Convert.ToInt32(Environment.GetEnvironmentVariable(EnvironmentVariables.SMTP_PORT)),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };
            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body
            })
            {
                try
                {
                    smtp.Send(message);
                    log.LogLine($"Email sent");
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    log.LogLine("Email failed sending code");
                    log.Log(ex.Message);
                    return ex.Message;
                }
            }            
        }

    }
}
