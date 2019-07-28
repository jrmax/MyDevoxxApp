using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDevoxx.Services
{
    interface INotificationService
    {
        Task<bool> TryRegister();

        void SubscribeTobic(string topic);

        void TryRegisterAndSubscribeKnownTopics();
    }
}
