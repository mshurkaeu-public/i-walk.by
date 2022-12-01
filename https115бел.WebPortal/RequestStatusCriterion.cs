using System;

namespace IWalkBy.https115бел.WebPortal
{
	[Flags]
	public enum RequestStatusCriterion
	{
		None = 0,

		//10
		//Новая заявка
		NewRequest = 1,

		//20
		//Назначен исполнитель
		PerformerIsSet = 2,

		//30
		//Проведено обследование
		SurveyConducted = 4,

		//40
		Unclear4 = 8,

		//10:20:30:40
		InWork = NewRequest | PerformerIsSet | SurveyConducted | Unclear4,

		//-20
		//На рассмотрении
		OnReview = 16,

		//35
		//В план текущего ремонта
		OnControl = 32,//назва ў фільтры не супадае з апісаннем статуса ў саміх заяўках...

		//-40
		//Отклонено
		Rejected = 64,

		//50
		//Заявка закрыта
		Closed = 128
	}
}