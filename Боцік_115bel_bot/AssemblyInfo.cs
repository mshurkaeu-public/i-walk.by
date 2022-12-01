using System;
using System.Diagnostics.CodeAnalysis;

[assembly: CLSCompliant(true)]

// гэта адключыць CA1707 для ўсёй assembly. У тым ліку для namespace. Як адключыць толькі для імя assembly - не ведаю.
//
// https://stackoverflow.com/questions/48922211/visual-studio-globalsuppressions-cs-prefix-p-for-attribute-target-in-suppres
//
// https://github.com/dotnet/roslyn/blob/315c2e149ba7889b0937d872274c33fcbfe9af5f/src/Compilers/Core/Portable/DiagnosticAnalyzer/SuppressMessageAttributeState.cs
[assembly: SuppressMessage(
	"Naming",
	"CA1707:Remove the underscores from assembly name",
	Justification = @"Я жадаю, каб бот называўся менавіта так - Боцік_115bel_bot.exe.
Таму што гэта вельмі блізка да названня бота ў Telegram. І таму, спадзяюся, інтуітыўна зразумела.",
	Scope = "module"
	)]
