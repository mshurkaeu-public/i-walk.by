using Newtonsoft.Json.Linq;
using System;

namespace IWalkBy.EdsPortal.RestfulWebService
{
	public class Notification
	{
		public int Id { get; private set; }
		public string RequestId { get; private set; }

		public Notification(JObject jObject)
		{
			/* напрыклад
				{
					"result_code": "RESULT_OK",
					"id_npm": 6171451,
					"is_read": 1,
					"created_at": "11.11.2022 17:20",
					"msg_header": "Заявка № 2045.6.040422 закрыта",
					"msg_body": "Нажмите, чтобы увидеть подробности",
					"msg_type": 2,
					"msg_load": "13864700",
					"msg_img": 4448211
				}
			*/
			if (jObject == null)
			{
				throw new ArgumentNullException(nameof(jObject));
			}

			string result_code = (string)jObject["result_code"];
			if (result_code != "RESULT_OK")
			{
				throw new NotImplementedException();
			}

			this.Id = (int)jObject["id_npm"];
			this.RequestId = (string)jObject["msg_load"];
		}
	}
}