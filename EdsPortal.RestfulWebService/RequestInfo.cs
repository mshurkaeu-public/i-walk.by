using Newtonsoft.Json.Linq;
using System;

namespace IWalkBy.EdsPortal.RestfulWebService
{
	public class RequestInfo
	{
		public int Id { get; private set; }
		public double? Latitude { get; private set; }
		public double? Longitude { get; private set; }
		public string Number { get; private set; }

		public RequestInfo(JObject jObject)
		{
			/* напрыклад
				{
					"result_code": "RESULT_OK",
					"id_request": 16629418,
					"cc_id": "2657.1.011122",
					"id_city": null,
					"city": null,
					"address": "Минск, Фрунзенский район, улица Одинцова, 5",
					"lat": 53.897914,
					"lng": 27.45957,
					"main_thumbnail": 4379656,
					"user_images": "4379656:4379658:4379659",
					"org_images": null,
					"create_date": "31.10.2022 23:04",
					"modify_date": "01.11.2022 11:01",
					"subject": "Не убирается дворовая территория от мусора",
					"user_comment": "Пешеходная дорожка зарастает травой. Прошу очистить, включая бордюр, и подмести.\r\n\r\nГлядзі фота і кропку на карце. Калі ласка, пасля выканання заяўкі зрабіце фота з такіх самых ракурсаў. Дзякуй.\r\n\r\n{\r\n\t\"c\": \"53.897914,27.459570\",\r\n\t\"Google карты\": \"https://www.google.com/maps/search/?api=1&query=53.897914,27.459570\",\r\n\t\"Яндекс карты\": \"https://yandex.ru/maps/?pt=27.459570,53.897914&z=17\",\r\n\t\"t\": \"6359202256edb6011d5f124a\",\r\n\t\"1\": \"16629418\"\r\n}",
					"hours_left": 0,
					"status": "В работе",
					"org_comment": null,
					"rating": null,
					"origin": "portal",
					"tstamp": "2022-10-31T20:04:07Z",
					"is_rework": 0
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

			this.Id = (int)jObject["id_request"];
			this.Latitude = (double?)jObject["lat"];
			this.Longitude = (double?)jObject["lng"];
			this.Number = (string)jObject["cc_id"];
		}
	}
}