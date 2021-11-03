using FaaSTestApp.Data;
using FaaSTestApp.Data.Entities;
using FaaSTestApp.Data.ExcelExport;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace FaaSTestApp
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private const string REPORT_DIRECTORY_NAME = "ReportExcels";
        private const int AZURE_CLOUD_NUMBER = 0;
        private const int AWS_CLOUD_NUMBER = 1;
        private const int GC_CLOUD_NUMBER = 2;
        private const string AZURE_REPORT_FILENAME = "Azure_Tests";
        private const string AWS_REPORT_FILENAME = "Aws_Tests";
        private const string GC_REPORT_FILENAME = "GoogleCloud_Tests";
        protected virtual void SetProperty<T>(ref T member, T val, [CallerMemberName] string propertyName = null)
        {
            if (Equals(member, val)) return;

            member = val;
            OnPropertyChanged(propertyName);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        #region Shared properties
        public double InfoBlockHeight { get => 200; }
        public double InfoBlockWidth { get => 400; }
        private bool _coldStartTesting;
        public bool ColdStartTesting
        {
            get => _coldStartTesting;
            set 
            {
                if(value)
                {
                    ShouldRequestsBeSynchronous = true;
                }
                SetProperty(ref _coldStartTesting, value);
            }
        }
        private bool _shouldRequestsBeSynchronous;
        public bool ShouldRequestsBeSynchronous
        {
            get => _shouldRequestsBeSynchronous;
            set => SetProperty(ref _shouldRequestsBeSynchronous, value);
        }
        private int _endpointRequestsNumberToMake;
        public int EndpointRequestsNumberToMake
        {
            get => _endpointRequestsNumberToMake;
            set => SetProperty(ref _endpointRequestsNumberToMake, value);
        }
        public HttpMethod[] HttpMehtodTypesCollection { get => new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete }; }

        public int PBMinValue { get => 0; }

        public Command StartCommand { get; set; }

        public BackgroundWorker _worker;
        #endregion Shared properties

        public MainWindowViewModel()
        {
            EndpointRequestsNumberToMake = 1;

            _worker = new BackgroundWorker();
            _worker.DoWork += _worker_DoWork;
            _worker.WorkerReportsProgress = true;
            _worker.ProgressChanged += _worker_ProgressChanged;
            _worker.RunWorkerCompleted += _worker_RunWorkerCompleted;

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            StartCommand = new Command(StartWorker);
        }

        public void StartWorker()
        {
            IsStartEnabled = false;
            AzurePBValue = 0;
            AWSPBValue = 0;
            GCPBValue = 0;
            _worker.RunWorkerAsync();
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            long sessionId;
            long azureResultId;
            long awsResultId;
            long gcResultId;

            using (var context = new AppDbContext())
            {
                var newSession = new TestSession()
                {
                    WasSynchronous = _shouldRequestsBeSynchronous,
                    WasColdStartTested = _coldStartTesting
                };

                context.Sessions.Add(newSession);
                context.SaveChanges();

                sessionId = newSession.Id;
            }
            using (var context = new AppDbContext())
            {
                var azureResult = new TestResult()
                {
                    Endpoint = _azureEndpoint,
                    HttpMethod = _azureRequestMethod.Method,
                    TestSessionId = sessionId
                };
                context.Results.Add(azureResult);

                var awsResult = new TestResult()
                {
                    Endpoint = _awsEndpoint,
                    HttpMethod = _awsRequestMethod.Method,
                    TestSessionId = sessionId
                };
                context.Results.Add(awsResult);

                var gcResult = new TestResult()
                {
                    Endpoint = _gcEndpoint,
                    HttpMethod = _gcRequestMethod.Method,
                    TestSessionId = sessionId
                };
                context.Results.Add(gcResult);

                context.SaveChanges();

                azureResultId = azureResult.Id;
                awsResultId = awsResult.Id;
                gcResultId = gcResult.Id;
            }

            BackgroundWorker worker = sender as BackgroundWorker;
            var httpClient = new HttpClient();

            var requests = new List<Task>();

            if (_shouldRequestsBeSynchronous)
            {
                int _20Minutes = 20 * 60 * 1000;
                for (int i = 0; i < EndpointRequestsNumberToMake; i++)
                {
                    requests.Add(PerformAsyncRequest(httpClient, new HttpRequestMessage(_azureRequestMethod, _azureEndpoint), azureResultId, worker, AZURE_CLOUD_NUMBER));
                    requests.Add(PerformAsyncRequest(httpClient, new HttpRequestMessage(_awsRequestMethod, _awsEndpoint), awsResultId, worker, AWS_CLOUD_NUMBER));
                    requests.Add(PerformAsyncRequest(httpClient, new HttpRequestMessage(_gcRequestMethod, _gcEndpoint), gcResultId, worker, GC_CLOUD_NUMBER));
                    Task.WaitAll(requests.ToArray());
                    if (_coldStartTesting)
                    {
                        Thread.Sleep(_20Minutes);
                    }
                }
            }
            else
            {
                for (int i = 0; i < EndpointRequestsNumberToMake; i++)
                {
                    requests.Add(PerformAsyncRequest(httpClient, new HttpRequestMessage(_azureRequestMethod, _azureEndpoint), azureResultId, worker, AZURE_CLOUD_NUMBER));
                    requests.Add(PerformAsyncRequest(httpClient, new HttpRequestMessage(_awsRequestMethod, _awsEndpoint), awsResultId, worker, AWS_CLOUD_NUMBER));
                    requests.Add(PerformAsyncRequest(httpClient, new HttpRequestMessage(_gcRequestMethod, _gcEndpoint), gcResultId, worker, GC_CLOUD_NUMBER));
                }

                Task.WaitAll(requests.ToArray());
            }

            UpdateAndExportResults(azureResultId, awsResultId, gcResultId);
        }

        private async Task PerformAsyncRequest(HttpClient httpClient, HttpRequestMessage httpRequestMessage, long testResultId, BackgroundWorker worker, int cloudNumber)
        {
            var sw = new Stopwatch();
            var request = new TestRequest();
            request.SentAt = DateTime.UtcNow;
            sw.Start();
            var result = await httpClient.SendAsync(httpRequestMessage);
            sw.Stop();
            request.RespondedAt = DateTime.UtcNow;
            request.ResponseTimeInMs = sw.ElapsedMilliseconds;
            request.HttpResponseCode = (int)result.StatusCode;
            request.TestResultId = testResultId;
            using (var context = new AppDbContext())
            {
                context.Requests.Add(request);
                context.SaveChanges();
            }
            worker.ReportProgress(0, cloudNumber);
        }

        private void UpdateAndExportResults(long azureResultId, long awsResultId, long gcResultId)
        {
            Directory.CreateDirectory(REPORT_DIRECTORY_NAME);

            using (var context = new AppDbContext())
            {

                var azureResult = context.Results
                                            .Include(r => r.Requests)
                                            .Include(r => r.TestSession)
                                            .FirstOrDefault(r => r.Id == azureResultId);
                CalculateSuccessAndAverageTime(azureResult);
                ExportResultToExcel(AZURE_REPORT_FILENAME, azureResult);

                var awsResult = context.Results
                                        .Include(r => r.Requests)
                                        .Include(r => r.TestSession)
                                        .FirstOrDefault(r => r.Id == awsResultId);
                CalculateSuccessAndAverageTime(awsResult);
                ExportResultToExcel(AWS_REPORT_FILENAME, awsResult);

                var gcResult = context.Results
                                        .Include(r => r.Requests)
                                        .Include(r => r.TestSession)
                                        .FirstOrDefault(r => r.Id == gcResultId);
                CalculateSuccessAndAverageTime(gcResult);
                ExportResultToExcel(GC_REPORT_FILENAME, gcResult);

                //Nulling children so Entity Framework CORE will not try to update/add them as they were loaded in previous methods
                azureResult.TestSession = null;
                azureResult.Requests = null;
                awsResult.TestSession = null;
                awsResult.Requests = null;
                gcResult.TestSession = null;
                gcResult.Requests = null;

                context.SaveChanges();
            }
        }

        private void CalculateSuccessAndAverageTime(TestResult result)
        {
            double summedMs = 0;
            bool wasSuccessful = true;
            foreach (var request in result.Requests)
            {
                summedMs += request.ResponseTimeInMs;
                if (request.HttpResponseCode > 299 || request.HttpResponseCode < 200)
                {
                    wasSuccessful = false;
                }
            }
            result.WasSuccessful = wasSuccessful;
            result.AverageResponseTimeInMs = summedMs / _endpointRequestsNumberToMake;
        }

        private void ExportResultToExcel(string resultName, TestResult result)
        {
            string wereOperationSynchronous = "_" + _shouldRequestsBeSynchronous.ToString().ToUpper();
            string wasColdStartTested = "_" + _coldStartTesting.ToString().ToUpper();
            string dateOfTests = "_" + DateTime.UtcNow.ToString();
            string filenameSuffix = (wereOperationSynchronous + wasColdStartTested + dateOfTests + ".xlsx").Replace(' ', '_').Replace(':', '_').Replace('-', '_');

            using (var package = new ExcelPackage(Path.Join(REPORT_DIRECTORY_NAME, resultName + filenameSuffix)))
            {
                package.Workbook.Worksheets.Add("Default");

                var headerRow = new List<string[]>()
                {
                    typeof(ExcelExportDto).GetProperties().Select(p => p.Name).ToArray()
                };

                string headerRange = "A1:" + Char.ConvertFromUtf32(headerRow[0].Length + 64) + "1";
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                worksheet.Cells[headerRange].LoadFromArrays(headerRow);

                var cellData = new List<object[]>();
                foreach(var request in result.Requests)
                {
                    cellData.Add(new object[] { 
                        request.SentAt,
                        result.Endpoint,
                        result.HttpMethod,
                        request.HttpResponseCode,
                        request.ResponseTimeInMs,
                        result.TestSession.WasSynchronous,
                        result.TestSession.WasColdStartTested,
                        result.TestSessionId
                    });
                }

                worksheet.Cells[2, 1].LoadFromArrays(cellData);

                package.Save();
            }
        }

        private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int changedCloud = (int)e.UserState;

            switch (changedCloud)
            {
                case AZURE_CLOUD_NUMBER:
                    AzurePBValue++;
                    break;
                case AWS_CLOUD_NUMBER:
                    AWSPBValue++;
                    break;
                case GC_CLOUD_NUMBER:
                    GCPBValue++;
                    break;
                default:
                    break;
            }
        }

        private void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            AzurePBValue = 0;
            AWSPBValue = 0;
            GCPBValue = 0;
            IsStartEnabled = true;
        }

        private bool _isStartEnabled;
        public bool IsStartEnabled
        {
            get => _isStartEnabled;
            set => SetProperty(ref _isStartEnabled, value);
        }

        #region Azure properties
        private HttpMethod _azureRequestMethod = HttpMethod.Get;
        public HttpMethod AzureRequestMethod
        {
            get => _azureRequestMethod;
            set => SetProperty(ref _azureRequestMethod, value);
        }
        private string _azureEndpoint;
        public string AzureEndpoint
        {
            get => _azureEndpoint;
            set => SetProperty(ref _azureEndpoint, value);
        }
        private int _azurePBValue;
        public int AzurePBValue
        {
            get => _azurePBValue;
            set => SetProperty(ref _azurePBValue, value);
        }
        #endregion Azure properties

        #region AWS properties
        private HttpMethod _awsRequestMethod = HttpMethod.Get;
        public HttpMethod AWSRequestMethod
        {
            get => _awsRequestMethod;
            set => SetProperty(ref _awsRequestMethod, value);
        }
        private string _awsEndpoint;
        public string AWSEndpoint
        {
            get => _awsEndpoint;
            set => SetProperty(ref _awsEndpoint, value);
        }
        private int _awsPBValue;
        public int AWSPBValue
        {
            get => _awsPBValue;
            set => SetProperty(ref _awsPBValue, value);
        }
        #endregion AWS properties

        #region GC properties
        private HttpMethod _gcRequestMethod = HttpMethod.Get;
        public HttpMethod GCRequestMethod
        {
            get => _gcRequestMethod;
            set => SetProperty(ref _gcRequestMethod, value);
        }
        private string _gcEndpoint;
        public string GCEndpoint
        {
            get => _gcEndpoint;
            set => SetProperty(ref _gcEndpoint, value);
        }
        private int _gcPBValue;
        public int GCPBValue
        {
            get => _gcPBValue;
            set => SetProperty(ref _gcPBValue, value);
        }
        #endregion GC properties

    }
}
