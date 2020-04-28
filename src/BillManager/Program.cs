using System;
using System.Collections.Generic;
using System.Text;
using BillSDK;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockSDK;
using UserSDK;

namespace BillManager
{
    internal class Request
    {
        public User User { get; set; }

        public List<ItemLine> Items { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "bill_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);
                var consumer = new EventingBasicConsumer(channel);
                channel.BasicConsume(queue: "bill_queue", autoAck: false, consumer: consumer);
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

                        var request = JsonConvert.DeserializeObject<Request>(message);

                        var bill = new Bill
                        {
                            User = request.User,
                            Lines = new List<BillLine>(),
                            TotalExclTax = 0,
                        };

                        foreach (var line in request.Items)
                        {
                            var subtotal = line.Quantity * line.Item.UnitPrice;
                            bill.Lines.Add(new BillLine
                            {
                                Item = line.Item,
                                Quantity = line.Quantity,
                                SubTotal = subtotal,
                            });
                            bill.TotalExclTax += subtotal;
                        }

                        bill.Total = bill.TotalExclTax * 1.15f;

                        response = JsonConvert.SerializeObject(bill);

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
