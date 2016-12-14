using System;
using System.Collections.Generic;
using System.Linq;

using Grpc.Core;

using Dd;

public static class Program {
    public static int Main(string[] args) {
        if(args.Length < 1) {
            Console.WriteLine("Usage: dotnet client.dll {PORT}");
            return -1;
        }
        int[] ports;
        try {
            ports = args.Select(arg => int.Parse(arg)).ToArray();
        } catch {
            Console.WriteLine("invalid format for port, use a number");
            return -1;
        }
        try {
            var channels = ports.Select(port => new Channel($"127.0.0.1:{port}", ChannelCredentials.Insecure)).ToArray();
            var clients = channels.Select(channel => new DictionaryService.DictionaryServiceClient(channel)).ToArray();
            var callOptions = new CallOptions(deadline: DateTime.UtcNow.Add(TimeSpan.FromSeconds(3)));

            for(var i = 1; i <= 5; ++i) {

                // Set key
                var key = "foo" + i;
                var value = "bar" + i;
                var setRequest = new SetRequest { Key = key, Value = value };
                var setResponse = clients.Set(setRequest, callOptions);
                Console.WriteLine($"setRequest={setRequest}");
                Console.WriteLine($"setResponse={setResponse}");
            }

            for(var i = 1; i <= 5; ++i) {

                // Retrieve key
                var key = "foo" + i;
                var getRequest = new GetRequest { Key = key };
                var getResponse = clients.Get(getRequest, callOptions);
                Console.WriteLine($"getRequest={getRequest}");
                Console.WriteLine($"getResponse={getResponse}");
            }

            // Get all keys
            var getAllKeys = new GetAllRequest();
            var getAllResponse = clients.GetAll(getAllKeys, callOptions);
            Console.WriteLine($"getAllKeys={getAllKeys}");
            Console.WriteLine($"getAllResponse={getAllResponse}");
            foreach(var channel in channels) {
                channel.ShutdownAsync().Wait();
            }
            Console.WriteLine("\nGRPC client exiting");

        } catch (Exception e) {
            Console.WriteLine($"There was an error: {e.Message}");
            return -1;
        }
        return 0;
    }
}

public static class DictionaryServiceClientEx {

    //--- Constants ---
    private const int REDUNDANCY = 2;

    //--- Extension Methods ---
    public static GetResponse Get(this DictionaryService.DictionaryServiceClient[] clients, GetRequest request, CallOptions callOptions) {
        var availableClients = clients.ToList();
        GetResponse response = null;
        for(var tries = 1; tries <= REDUNDANCY; ++tries) {
            var client = GetClient(request.Key, availableClients.ToArray());
            try {
                response = client.Get(request, callOptions);
                if(response.Found) {
                    return response;
                }
            } catch {
                availableClients.Remove(client);
            }
        }
        if(response == null) {
            throw new Exception("get oops");
        }
        return response;
    }

    public static SetResponse Set(this DictionaryService.DictionaryServiceClient[] clients, SetRequest request, CallOptions callOptions) {
        var availableClients = clients.ToList();
        SetResponse response = null;
        for(var tries = 1; tries <= REDUNDANCY; ++tries) {
            var client = GetClient(request.Key, availableClients.ToArray());
            try {
                response = client.Set(request, callOptions) ?? response;
            } catch {
                availableClients.Remove(client);
            }
        }
        if(response == null) {
            throw new Exception("set oops");
        }
        return response;
    }

    public static GetAllResponse GetAll(this DictionaryService.DictionaryServiceClient[] clients, GetAllRequest request, CallOptions callOptions) {
        var keys = new HashSet<string>();
        foreach(var client in clients) {
            try {
                foreach(var key in client.GetAll(request, callOptions).Keys) {
                    keys.Add(key);
                }
            } catch {

                // ignore error and continue
                continue;
            }
        }
        var response = new GetAllResponse();
        response.Keys.AddRange(keys);
        return response;
    }

    private static DictionaryService.DictionaryServiceClient GetClient(string key, DictionaryService.DictionaryServiceClient[] clients) {
        var index = key.GetHashCode() % clients.Length;
        if(index < 0) {
            index += clients.Length;
        }
        return clients[index];
    }
}