using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockSDK;
using UserSDK;

namespace BillSDK
{
    internal class Request
    {
        public User User { get; set; }

        public List<ItemLine> Items { get; set; }
    }

    public class Bill
    {
        public User User { get; set; }

        public List<BillLine> Lines { get; set; }

        public float TotalExclTax { get; set; }

        public float Total { get; set; }

        public static Bill CreateBill(User user, List<ItemLine> lines)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var replyQueueName = channel.QueueDeclare().QueueName;
            var consumer = new EventingBasicConsumer(channel);

            var props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;

            BlockingCollection<string> respQueue = new BlockingCollection<string>();

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var response = Encoding.UTF8.GetString(body);
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    respQueue.Add(response);
                }
            };

            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Request { User = user, Items = lines }));
            channel.BasicPublish(
                exchange: "",
                routingKey: "bill_queue",
                basicProperties: props,
                body: messageBytes);

            channel.BasicConsume(consumer: consumer, queue: replyQueueName, autoAck: true);

            string responseContent = respQueue.Take();

            connection.Close();

            if (responseContent.Trim().Length > 0)
            {
                return JsonConvert.DeserializeObject<Bill>(responseContent);
            }

            return null;
        }
    }
}
