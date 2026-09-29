using WebGoatCore.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WebGoatCore.Data
{
    public class OrderRepository
    {
        private readonly NorthwindContext _context;
        private readonly CustomerRepository _customerRepository;

        public OrderRepository(NorthwindContext context, CustomerRepository customerRepository)
        {
            _context = context;
            _customerRepository = customerRepository;
        }

        public Order GetOrderById(int orderId)
        {
            return _context.Orders.Single(o => o.OrderId == orderId);
        }

        public int CreateOrder(Order order)
        {
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = @"INSERT INTO Orders (
                    CustomerId, EmployeeId, OrderDate, RequiredDate, ShippedDate, ShipVia, Freight, ShipName, ShipAddress,
                    ShipCity, ShipRegion, ShipPostalCode, ShipCountry
                ) VALUES (
                    @CustomerId, @EmployeeId, @OrderDate, @RequiredDate, @ShippedDate, @ShipVia, @Freight, @ShipName,
                    @ShipAddress, @ShipCity, @ShipRegion, @ShipPostalCode, @ShipCountry
                );
                SELECT OrderID FROM Orders ORDER BY OrderID DESC LIMIT 1;";
                AddParameter(command, "@CustomerId", order.CustomerId);
                AddParameter(command, "@EmployeeId", order.EmployeeId);
                AddParameter(command, "@OrderDate", order.OrderDate);
                AddParameter(command, "@RequiredDate", order.RequiredDate);
                AddParameter(command, "@ShippedDate", order.ShippedDate);
                AddParameter(command, "@ShipVia", order.ShipVia);
                AddParameter(command, "@Freight", order.Freight);
                AddParameter(command, "@ShipName", order.ShipName);
                AddParameter(command, "@ShipAddress", order.ShipAddress);
                AddParameter(command, "@ShipCity", order.ShipCity);
                AddParameter(command, "@ShipRegion", order.ShipRegion);
                AddParameter(command, "@ShipPostalCode", order.ShipPostalCode);
                AddParameter(command, "@ShipCountry", order.ShipCountry);
                _context.Database.OpenConnection();

                using var dataReader = command.ExecuteReader();
                dataReader.Read();
                order.OrderId = Convert.ToInt32(dataReader[0]);
            }

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                var values = new List<string>();
                foreach (var (orderDetails, i) in order.OrderDetails.WithIndex())
                {
                    orderDetails.OrderId = order.OrderId;
                    values.Add($"(@OrderId{i}, @ProductId{i}, @UnitPrice{i}, @Quantity{i}, @Discount{i})");
                    AddParameter(command, $"@OrderId{i}", orderDetails.OrderId);
                    AddParameter(command, $"@ProductId{i}", orderDetails.ProductId);
                    AddParameter(command, $"@UnitPrice{i}", orderDetails.UnitPrice);
                    AddParameter(command, $"@Quantity{i}", orderDetails.Quantity);
                    AddParameter(command, $"@Discount{i}", orderDetails.Discount);
                }

                command.CommandText = "INSERT INTO OrderDetails " +
                    "(OrderId, ProductId, UnitPrice, Quantity, Discount) VALUES " +
                    string.Join(",", values);

                if (order.Shipment != null)
                {
                    var shipment = order.Shipment;
                    shipment.OrderId = order.OrderId;
                    command.CommandText += ";\nINSERT INTO Shipments " +
                        "(OrderId, ShipperId, ShipmentDate, TrackingNumber) VALUES " +
                        "(@ShipmentOrderId, @ShipperId, @ShipmentDate, @TrackingNumber)";
                    AddParameter(command, "@ShipmentOrderId", shipment.OrderId);
                    AddParameter(command, "@ShipperId", shipment.ShipperId);
                    AddParameter(command, "@ShipmentDate", shipment.ShipmentDate);
                    AddParameter(command, "@TrackingNumber", shipment.TrackingNumber);
                }

                _context.Database.OpenConnection();
                command.ExecuteNonQuery();
            }

            return order.OrderId;
        }

        private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        public void CreateOrderPayment(int orderId, decimal amountPaid, string creditCardNumber, DateTime expirationDate, string approvalCode)
        {
            var orderPayment = new OrderPayment()
            {
                AmountPaid = Convert.ToDouble(amountPaid),
                CreditCardNumber = creditCardNumber,
                ApprovalCode = approvalCode,
                ExpirationDate = expirationDate,
                OrderId = orderId,
                PaymentDate = DateTime.Now
            };
            _context.OrderPayments.Add(orderPayment);
            _context.SaveChanges();
        }

        public ICollection<Order> GetAllOrdersByCustomerId(string customerId)
        {
            return _context.Orders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId)
                .ToList();
        }
    }
}
