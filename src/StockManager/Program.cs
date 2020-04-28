using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StockManager
{
    public class StockItem
    {
        public string Name { get; set; }

        public float UnitPrice { get; set; }

        public uint Quantity { get; set; }
    }

    public class ChangeRequest
    {
        public string Action { get; set; }

        public string Name { get; set; }

        public uint Quantity { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), "products.json");
            string filecontent = File.ReadAllText(filePath);
            Dictionary<string, StockItem> products = JsonConvert.DeserializeObject<List<StockItem>>(filecontent).ToDictionary(x => x.Name, x => x);

            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "stock_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);
                var consumer = new EventingBasicConsumer(channel);
                channel.BasicConsume(queue: "stock_queue", autoAck: false, consumer: consumer);
                Console.WriteLine("[x] Awaiting RPC requests");

                consumer.Received += (model, ea) =>
                {
                    string response = null;

                    var body = ea.Body;
                    var props = ea.BasicProperties;
                    var replyProps = channel.CreateBasicProperties();
                    replyProps.CorrelationId = props.CorrelationId;

                    try
                    {
                        var message = Encoding.UTF8.GetString(body).Trim();

                        var request = JsonConvert.DeserializeObject<ChangeRequest>(message);

                        if(request.Action == "increment")
                        {
                            if (products.ContainsKey(request.Name))
                            {
                                products[request.Name].Quantity += request.Quantity;
                                response = JsonConvert.SerializeObject(products[request.Name]);
                            }
                        }
                        else if(request.Action == "decrement")
                        {
                            if (products.ContainsKey(request.Name))
                            {
                                products[request.Name].Quantity += request.Quantity;
                                response = JsonConvert.SerializeObject(products[request.Name]);
                            }
                        }

                        if(response == null)
                        {
                            response = "";
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(" [.] " + e.Message);
                        response = "";
                    }
                    finally
                    {
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, basicProperties: replyProps, body: responseBytes);
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                };

                while(true);
            }
        }
    }
}
