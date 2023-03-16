using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Contexts;
using Newtonsoft.Json;
using System.IO;
using log4net;
using log4net.Repository.Hierarchy;
using System.Diagnostics;
using System.Reflection.Emit;
using log4net.Core;
using System.Web.UI.WebControls;
using System.Collections;

namespace CalculatorServer
{

    public class CalculatorController
    {
        // A stack to store arguments for stack-based operations
        private Stack<int> argumentStack = new Stack<int>();
        HttpListener listener = new HttpListener();
        HttpListenerContext context = null;
        HttpListenerResponse response = null;
        int requestCounter = 1;
        private ILog requestLog = LogManager.GetLogger("request-logger");
        private ILog stackLog = LogManager.GetLogger("stack-logger");
        private ILog independentLog = LogManager.GetLogger("independent-logger");

        // A dictionary that maps operation names to their corresponding functions
        private Dictionary<string, Func<int[], int>> operations = new Dictionary<string, Func<int[], int>>
        {
            {"plus", args => args[0] + args[1]},
            {"minus", args => args[0] - args[1]},
            {"times", args => args[0] * args[1]},
            {"divide", args => args[0] / args[1]},
            {"pow", args => (int) Math.Pow(args[0], args[1])},
            {"abs", args => Math.Abs(args[0])},
            {"fact", args => Factorial(args[0])},
        };


        public CalculatorController()
        {
            //9583
            listener.Prefixes.Add("http://localhost:9583/");
            listener.Start();
            string logsDirectory = "../../../../logs";
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }
            log4net.Config.XmlConfigurator.Configure();
        }

        public void RunCalculator()
        {
            context = listener.GetContext();
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            response = context.Response;
            string reqString = context.Request.Url.LocalPath;
            requestLog.Info(string.Format("Incoming request | #{0} | resource: {1} | HTTP Verb {2} | request #{0}", requestCounter, reqString, context.Request.HttpMethod));
           
            if (reqString == "/independent/calculate")
            {
                independentCalc();
            }
            else if (reqString == "/stack/size")
            {
                getStackSize();
                stackLog.Info(string.Format("Stack size is {0} | request #{1}", argumentStack.Count, requestCounter));
                stackLog.Debug(string.Format("Stack content (first == top): [{0}] | request #{1}", string.Join(", ", argumentStack), requestCounter));
            }
            else if (reqString == "/stack/arguments")
            {
                manageArguments();
            }
            else if (reqString == "/stack/operate")
            {
                performOperation();
            }
            else if (reqString == "/logs/level")
            {
                manageLogsLevel();
            }
            stopWatch.Stop();
            requestLog.Debug(string.Format("request #{0} duration: {1}ms | request #{0}", requestCounter, stopWatch.ElapsedMilliseconds));
            requestCounter++;
        }

        private void manageLogsLevel()
        {
            string loggerNameQuery = context.Request.QueryString["logger-name"];
            string loggerLevelQuery = context.Request.QueryString["logger-level"];
            bool loggerNameQueryExits = checkLoggerNameQuery(loggerNameQuery);

            if (loggerNameQueryExits)
            {
                if (context.Request.HttpMethod == "GET")
                {
                    createSuccessResponseMessage(loggerNameQuery);
                }
                else if (context.Request.HttpMethod == "PUT")
                {
                    changeLoggerLevelByQuery(loggerNameQuery, loggerLevelQuery);
                }
            }
            else
            {
                createFailureResponseMessage("wrong logger name in query");
            }
        }

        private bool checkLoggerNameQuery(string loggerNameQuery)
        {
            return loggerNameQuery == "request-logger" || loggerNameQuery == "stack-logger" || loggerNameQuery == "independent-logger";
        }

        private void changeLoggerLevelByQuery(string loggerNameQuery, string loggerLevelQuery)
        {

            Level level = Level.Info;
            bool validLoggerLevel = true;
            switch (loggerLevelQuery.ToLowerInvariant())
            {
                case "debug":
                    level = Level.Debug;
                    break;
                case "info":
                    level = Level.Info;
                    break;
                case "warn":
                    level = Level.Warn;
                    break;
                case "error":
                    level = Level.Error;
                    break;
                case "fatal":
                    level = Level.Fatal;
                    break;
                default:
                    validLoggerLevel = false;
                    createFailureResponseMessage("wrong logger level in query");
                    break;
            }
            if (validLoggerLevel)
            {
                if(loggerNameQuery == "request-logger")
                {
                    ((Logger)requestLog.Logger).Level = level;
                }
                else if (loggerNameQuery == "stack-logger")
                {
                    ((Logger)stackLog.Logger).Level = level;
                }
                else if(loggerNameQuery == "independent-logger")
                {
                    ((Logger)independentLog.Logger).Level = level;
                }
                createSuccessResponseMessage(string.Format("{0} level changed to {1} successfully!", loggerNameQuery, loggerLevelQuery.ToUpper()));
            }
        }

        private void independentCalc()
        {
            string strData = extractData();
            Dictionary<string, object> body = JsonConvert.DeserializeObject<Dictionary<string, object>>(strData);
            string argOperation = (body["operation"] as string).ToLower();
            int[] argsNumberArr = JsonConvert.DeserializeObject<int[]>(body["arguments"].ToString());

            string notEnoughArgsErrorMessage = string.Format("Error: Not enough arguments to perform the operation {0}", argOperation);
            makeOperation(argOperation, argsNumberArr, notEnoughArgsErrorMessage, false);
        }

        private void getStackSize()
        {
            createSuccessResponseMessage(argumentStack.Count);
           
        }

        private void manageArguments()
        {
            if (context.Request.HttpMethod == "PUT")
            {
                addArguments();
            }
            else if (context.Request.HttpMethod == "DELETE")
            {
                deleteArguments();
            }
        }

        private void addArguments()
        {
            string strData = extractData();
            int countArgsAdded = 0;
            List<int> argsAdded = new List<int>();
            try
            {
                Dictionary<string, List<int>> line = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(strData);
                foreach (int argument in line["arguments"])
                {
                    argumentStack.Push(argument);
                    argsAdded.Add(argument);
                    countArgsAdded++;
                }
                getStackSize();
                stackLog.Info(string.Format("Adding total of {0} argument(s) to the stack | Stack size: {1} | request #{2}", countArgsAdded, argumentStack.Count, requestCounter));
                stackLog.Debug(string.Format("Adding arguments: {0} | Stack size before {1} | stack size after {2} | request #{3}", string.Join(",", argsAdded), argumentStack.Count - countArgsAdded, argumentStack.Count, requestCounter));
            }
            catch (Exception)
            {
                createFailureResponseMessage("");
                writeFailureToLoggers("adding arguments operation has failed!", true);
            }

        }

        private void deleteArguments()
        {
            try
            {
                int argNumToDelete = int.Parse(context.Request.QueryString.GetValues("count")[0]);
                if (argNumToDelete <= argumentStack.Count)
                {
                    for (int i = 0; i < argNumToDelete; i++)
                    {
                        argumentStack.Pop();
                    }
                    getStackSize();
                    stackLog.Info(string.Format("Removing total {0} argument(s) from the stack | Stack size: {1} | request #{2}", argNumToDelete, argumentStack.Count, requestCounter));
                }
                else
                {
                    string errorMessage = string.Format("Error: cannot remove {0} from the stack. It has only {1} arguments", argNumToDelete, argumentStack.Count);
                    createFailureResponseMessage(errorMessage);
                    writeFailureToLoggers(errorMessage, true);
                }
            }
            catch (Exception)
            {
                createFailureResponseMessage("");
                writeFailureToLoggers("removing arguments operation has failed!", true);
            }

        }

        private void performOperation()
        {
            string argOperation = context.Request.QueryString.GetValues("operation")[0].ToLower();
            string notEnoughArgsErrorMessage = string.Format("Error: cannot implement operation {0}. It requires 2 arguments and the stack has only {1} arguments", argOperation, argumentStack.Count);
            makeOperation(argOperation, argumentStack.ToArray(), notEnoughArgsErrorMessage, true);
        }

        private void makeOperation(string valueOp, int[] argsNumbersArr, string notEnoughArgsErrorMessage, bool argsFromStack)
        {
            try
            {
                if (!argsFromStack)
                {
                    if ((argsNumbersArr.Length > 1 && (valueOp == "fact" || valueOp == "abs")) || (argsNumbersArr.Length > 2))
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                }
                int result = operations[valueOp](argsNumbersArr);
                createSuccessResponseMessage(result);
                writePerformSuccessToLoggers(valueOp, argsNumbersArr, result, argsFromStack);
                popArgsFromStack(valueOp, argsFromStack);

            }
            catch (DivideByZeroException)
            {
                operateFailureResponse("Error while performing operation Divide: division by 0", argsFromStack);
            }
            catch (ArithmeticException)
            {
                operateFailureResponse("Error while performing operation Factorial: not supported for the negative number", argsFromStack);
            }
            catch (ArgumentOutOfRangeException)
            {
                operateFailureResponse(string.Format("Error: Too many arguments to perform the operation {0}", valueOp), argsFromStack);
            }
            catch (ArgumentException)
            {
                operateFailureResponse(string.Format("Error: unknown operation: {0}", valueOp), argsFromStack);
            }
            catch (Exception)
            {
                operateFailureResponse(notEnoughArgsErrorMessage, argsFromStack);
            }
        }

        private void operateFailureResponse(string errorMessage, bool argsFromStack)
        {
            createFailureResponseMessage(errorMessage);
            writeFailureToLoggers(errorMessage, argsFromStack);
        }

        private void popArgsFromStack(string argOperation, bool argsFromStack)
        {
            if (argsFromStack)
            {
                if (argOperation == "fact" || argOperation == "abs")
                {
                    if (argumentStack.Count > 0)
                    {
                        argumentStack.Pop();
                    }

                }
                else if (argOperation == "plus" || argOperation == "minus" || argOperation == "times" || argOperation == "divide" || argOperation == "pow")
                {
                    if (argumentStack.Count > 1)
                    {
                        argumentStack.Pop();
                        argumentStack.Pop();
                    }

                }
            }
        }

        private void writePerformSuccessToLoggers(string valueOp, int[] argsNumbersArr, int result, bool argsFromStack)
        {
            if(argsFromStack)
            {
                
                if (valueOp == "fact" || valueOp == "abs")
                {
                    stackLog.Info(string.Format("Performing operation {0}. Result is {1} | stack size: {2} | request #{3}", valueOp, result, argumentStack.Count - 1, requestCounter));
                    stackLog.Debug(string.Format("Performing operation: {0}({1}) = {2} | request #{3}", valueOp, argsNumbersArr[0], result, requestCounter));
                }
                else
                {
                    stackLog.Info(string.Format("Performing operation {0}. Result is {1} | stack size: {2} | request #{3}", valueOp, result, argumentStack.Count - 2, requestCounter));
                    stackLog.Debug(string.Format("Performing operation: {0}({1},{2}) = {3} | request #{4}", valueOp, argsNumbersArr[0], argsNumbersArr[1], result, requestCounter));
                }

            }
            else
            {
                independentLog.Info(string.Format("Performing operation {0}. Result is {1} | request #{2}", valueOp, result, requestCounter));
                independentLog.Debug(string.Format("Performing operation: {0}({1}) = {2} | request #{3}", valueOp, string.Join(",", argsNumbersArr), result, requestCounter));
            }
        }

        private void writeFailureToLoggers(string errorMessage, bool argsFromStack)
        {
            if (argsFromStack)
            {
                stackLog.Error(string.Format("Server encountered an error ! message: {0} | request #{1}", errorMessage, requestCounter));
            }
            else
            {
                independentLog.Error(string.Format("Server encountered an error ! message: {0} | request #{1}", errorMessage, requestCounter));
            }
        }

        private string extractData()
        {
            Stream body = context.Request.InputStream;
            Encoding encoding = context.Request.ContentEncoding;
            StreamReader reader = new StreamReader(body, encoding);
            return reader.ReadToEnd();
        }

        private void createSuccessResponseMessage(int resultData)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            result.Add("result", resultData);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.StatusDescription = "OK";
            var responseBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
            response.ContentLength64 = responseBody.Length;
            response.OutputStream.Write(responseBody, 0, responseBody.Length);

        }

        private void createSuccessResponseMessage(string resultLevel)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            result.Add("level", resultLevel);

            response.StatusCode = (int)HttpStatusCode.OK;
            response.StatusDescription = "OK";
            var responseBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
            response.ContentLength64 = responseBody.Length;
            response.OutputStream.Write(responseBody, 0, responseBody.Length);

        }

        private void createFailureResponseMessage(string errorMessage)
        {
            Dictionary<string, string> errorResult = new Dictionary<string, string>();
            errorResult.Add("error-message", errorMessage);

            response.StatusCode = (int)HttpStatusCode.Conflict;
            response.StatusDescription = "Conflict";
            var responseBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(errorResult));
            response.ContentLength64 = responseBody.Length;
            response.OutputStream.Write(responseBody, 0, responseBody.Length);

        }

        // A helper function to calculate the factorial of a number
        private static int Factorial(int x)
        {
            if (x < 0)
            {
                throw new ArithmeticException();
            }
            if (x <= 1)
                return 1;
            return x * Factorial(x - 1);
        }

    }
}

