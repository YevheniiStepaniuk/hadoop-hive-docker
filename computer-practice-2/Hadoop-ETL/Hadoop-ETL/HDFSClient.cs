﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Hadoop_ETL.Infrastructure;

namespace Hadoop_ETL
{
    public class HDFSClient
    {
        private readonly HttpClient _httpClient;

        private readonly HadoopOptions _options;

        public HDFSClient(HadoopOptions options)
        {
            _options = options;

            var defaultHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            var stopWatchHandler = new StopwatchHandler(defaultHandler);
            var queryParamHandler = new QueryParamAppendHandler("user.name", _options.UserName, stopWatchHandler);

            _httpClient = new HttpClient(queryParamHandler)
            {
                BaseAddress = new Uri(_options.ServerUrl),
            };
        }

        public Task<bool> UploadFile(string path, string filePath, bool overwrite = false)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File doesn't exist at: {filePath}");
                return Task.FromResult(false);
            }

            var stream = File.OpenRead(filePath);

            return UploadFile(path, stream, overwrite);
        }

        public async Task<bool> UploadFile(string path, Stream stream, bool overwrite = false)
        {
            var url = BuildRequestLink(path, HadoopActions.CREATE, ("overwrite", overwrite.ToString()));

            var locationResponse = await _httpClient.PutAsync(url, null);
            var location = locationResponse.Headers.Location;
            try
            {
                await _httpClient.PutAsync(location, new StreamContent(stream));
            }
            catch (Exception e)
            {
                e.WriteToConsole();
                return false;
            }

            return true;
        }

        public async Task<bool> CreateDirectory(string path)
        {
            var url = BuildRequestLink(path, HadoopActions.MKDIRS);

            try
            {
                await _httpClient.PutAsync(url, null);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> DirectoryExist(string path)
        {
            var url = BuildRequestLink(path, HadoopActions.GETFILESTATUS);
            try
            {
               var response = await _httpClient.GetAsync(url);
               return response.IsSuccessStatusCode;
            }
            catch (Exception e)
            {
                e.WriteToConsole();
                return false;
            }
        }

        private string BuildRequestLink(string path, HadoopActions action, params (string, string)[] values)
        {
            var query = values.IntoDictionary(("op", $"{action:G}"));

            return $"{_options.ServerUrl}{path}{query.ToUrl()}";
        }
    }

    public enum HadoopActions
    {
        APPEND,
        MKDIRS,
        CREATE,
        GETFILESTATUS
    }
}