using System;
using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StockSDK
{
    internal class ChangeRequest
    {
        public string Action { get; set; }

        public string Name { get; set; }

        public uint Quantity { get; set; }
    }

    public static class StockManager
    {
        public static ItemLine ReserveItem(uint quantity, string name)
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

            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new ChangeRequest { Action = "decrement", Name = name, Quantity = quantity }));
            channel.BasicPublish(
                exchange: "",
                routingKey: "stock_queue",
                basicProperties: props,
                body: messageBytes);

            channel.BasicConsume(consumer: consumer, queue: replyQueueName, autoAck: true);

            string responseContent = respQueue.Take();

            connection.Close();

            if (responseContent.Trim().Length > 0)
            {
                return new ItemLine 
                {
                    Item = JsonConvert.DeserializeObject<Item>(responseContent),
                    Quantity = quantity,
                };
            }

            return null;
        }
        
        public static void ReleaseItem(ItemLine line)
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

            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new ChangeRequest { Action = "increment", Name = line.Item.Name, Quantity = line.Quantity }));
            channel.BasicPublish(
                exchange: "",
                routingKey: "stock_queue",
                basicProperties: props,
                body: messageBytes);

            channel.BasicConsume(consumer: consumer, queue: replyQueueName, autoAck: true);

            connection.Close();
        }
    }
}
