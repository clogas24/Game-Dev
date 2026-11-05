// Copyright Epic Games, Inc. All Rights Reserved.

#include "StopwatchMenuWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Brushes/SlateRoundedBoxBrush.h"
#include "Components/Border.h"
#include "Components/Button.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Engine/World.h"
#include "PlaytestLogger.h"
#include "StopwatchGameMode.h"
#include "StopwatchPlayerController.h"

namespace
{
	const FLinearColor PanelColor(0.02f, 0.02f, 0.03f, 0.92f);
	const FLinearColor TitleColor(1.0f, 0.78f, 0.35f, 1.0f);
	const FLinearColor BodyColor(1.0f, 1.0f, 1.0f, 1.0f);
	const FLinearColor DimColor(0.6f, 0.6f, 0.6f, 1.0f);
	const FMargin RowPadding(0.0f, 5.0f);

	FText FormatTime(double Seconds)
	{
		const int32 Whole = FMath::FloorToInt(Seconds);
		return FText::FromString(FString::Printf(TEXT("%d:%02d"), Whole / 60, Whole % 60));
	}
}

void UStopwatchMenuWidget::NativeOnInitialized()
{
	Super::NativeOnInitialized();

	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;

	UBorder* Panel = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	Panel->SetBrush(FSlateRoundedBoxBrush(FLinearColor::White, 10.0f));
	Panel->SetBrushColor(PanelColor);
	Panel->SetPadding(FMargin(36.0f, 28.0f));
	UCanvasPanelSlot* PanelSlot = Canvas->AddChildToCanvas(Panel);
	PanelSlot->SetAnchors(FAnchors(0.5f, 0.5f));
	PanelSlot->SetAlignment(FVector2D(0.5f, 0.5f));
	PanelSlot->SetAutoSize(true);

	Rows = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Panel->SetContent(Rows);
}

void UStopwatchMenuWidget::AddText(const FText& Text, int32 FontSize, const FLinearColor& Color)
{
	UTextBlock* Block = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	FSlateFontInfo Font = Block->GetFont();
	Font.Size = FontSize;
	Block->SetFont(Font);
	Block->SetText(Text);
	Block->SetColorAndOpacity(FSlateColor(Color));
	Block->SetJustification(ETextJustify::Center);
	Rows->AddChildToVerticalBox(Block)->SetPadding(RowPadding);
}

UButton* UStopwatchMenuWidget::AddButton(const FText& Label, FName Handler)
{
	UButton* Button = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
	UTextBlock* Text = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	Text->SetText(Label);
	Text->SetColorAndOpacity(FSlateColor(FLinearColor::Black));
	Text->SetJustification(ETextJustify::Center);
	Button->SetContent(Text);

	FScriptDelegate Delegate;
	Delegate.BindUFunction(this, Handler);
	Button->OnClicked.Add(Delegate);

	UVerticalBoxSlot* ButtonSlot = Rows->AddChildToVerticalBox(Button);
	ButtonSlot->SetPadding(FMargin(80.0f, 4.0f));
	ButtonSlot->SetHorizontalAlignment(HAlign_Fill);
	return Button;
}

void UStopwatchMenuWidget::ShowTitle()
{
	Rows->ClearChildren();

	// one row per action, the key then the button, so the controls read as a table
	const AStopwatchPlayerController* Controller = GetOwningPlayer<AStopwatchPlayerController>();
	auto Keys = [Controller](const TCHAR* Token)
	{
		const FText Prompt = FText::FromString(FString::Printf(TEXT("{%s}"), Token));
		return Controller ? FText::Format(NSLOCTEXT("Stopwatch", "KeyPair", "{0}  /  {1}"), Controller->FormatPrompt(Prompt, false), Controller->FormatPrompt(Prompt, true)) : FText::GetEmpty();
	};

	AddText(NSLOCTEXT("Stopwatch", "GameName", "STOPWATCH"), 64, TitleColor);
	AddText(NSLOCTEXT("Stopwatch", "Premise", "You're a clockmaker's apprentice. Freeze and rewind clockwork to climb the collapsing tower."), 18, BodyColor);
	AddText(FText::GetEmpty(), 8, BodyColor);
	AddText(NSLOCTEXT("Stopwatch", "ControlMove", "Move   WASD  /  left stick        Look   mouse  /  right stick"), 16, DimColor);
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "ControlJump", "Jump   {0}        Pause   {1}"), Keys(TEXT("jump")), Keys(TEXT("pause"))), 16, DimColor);
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "ControlTime", "Freeze   {0}        Rewind   {1}"), Keys(TEXT("freeze")), Keys(TEXT("rewind"))), 16, DimColor);
	AddText(FText::GetEmpty(), 8, BodyColor);
	AddText(NSLOCTEXT("Stopwatch", "ControlsAim", "Aim the dot at brass clockwork. Both powers cost sand, and hourglasses refill it."), 16, BodyColor);
	AddText(FText::GetEmpty(), 8, BodyColor);
	AddText(NSLOCTEXT("Stopwatch", "PressAnyKey", "Press any key  /  Start"), 24, TitleColor);
}

void UStopwatchMenuWidget::ShowPause(const FString& LogFolder)
{
	Rows->ClearChildren();

	AddText(NSLOCTEXT("Stopwatch", "PausedTitle", "PAUSED"), 40, TitleColor);
	UButton* Resume = AddButton(NSLOCTEXT("Stopwatch", "Resume", "Resume"), GET_FUNCTION_NAME_CHECKED(UStopwatchMenuWidget, HandleResume));
	AddButton(NSLOCTEXT("Stopwatch", "Restart", "Restart"), GET_FUNCTION_NAME_CHECKED(UStopwatchMenuWidget, HandleRestart));
	AddButton(NSLOCTEXT("Stopwatch", "Quit", "Quit"), GET_FUNCTION_NAME_CHECKED(UStopwatchMenuWidget, HandleQuit));
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "LogFolder", "Playtest logs: {0}"), FText::FromString(LogFolder)), 12, DimColor);

	// so a gamepad can pick a button straight away
	if (APlayerController* Controller = GetOwningPlayer())
	{
		Resume->SetUserFocus(Controller);
	}
}

void UStopwatchMenuWidget::ShowWon(const FPlaytestStats& Totals, const FString& LogFolder)
{
	Rows->ClearChildren();

	AddText(NSLOCTEXT("Stopwatch", "WonTitle", "THE CLOCK STRIKES. YOU MADE IT TO THE TOP"), 36, TitleColor);
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "WonTime", "Time   {0}"), FormatTime(Totals.Seconds)), 22, BodyColor);
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "WonDeaths", "Deaths   {0}      Fell {1}   ·   Crushed {2}   ·   Blade {3}   ·   Collapse {4}"),
		Totals.GetTotalDeaths(), Totals.GetDeaths(EDeathCause::Fall), Totals.GetDeaths(EDeathCause::Crushed), Totals.GetDeaths(EDeathCause::Blade), Totals.GetDeaths(EDeathCause::Collapse)), 18, BodyColor);
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "WonSand", "Sand spent   {0}      Freezes {1}   ·   Rewinds {2}"),
		FMath::RoundToInt(Totals.SandSpent), Totals.Freezes, Totals.Rewinds), 18, BodyColor);
	AddText(FText::GetEmpty(), 8, BodyColor);
	AddText(NSLOCTEXT("Stopwatch", "PlayAgain", "R  /  A   to play again"), 22, TitleColor);

	// not focused, so A restarts instead of pressing Quit
	AddButton(NSLOCTEXT("Stopwatch", "Quit", "Quit"), GET_FUNCTION_NAME_CHECKED(UStopwatchMenuWidget, HandleQuit));
	AddText(FText::Format(NSLOCTEXT("Stopwatch", "LogFolder", "Playtest logs: {0}"), FText::FromString(LogFolder)), 12, DimColor);
}

FString UStopwatchMenuWidget::GetScreenText() const
{
	FString Text;

	WidgetTree->ForEachWidget([&Text](UWidget* Widget)
	{
		if (const UTextBlock* Block = Cast<UTextBlock>(Widget))
		{
			Text += Block->GetText().ToString() + TEXT("\n");
		}
	});

	return Text;
}

void UStopwatchMenuWidget::HandleResume()
{
	if (AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>())
	{
		GameMode->TogglePause();
	}
}

void UStopwatchMenuWidget::HandleRestart()
{
	if (AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>())
	{
		GameMode->Restart();
	}
}

void UStopwatchMenuWidget::HandleQuit()
{
	if (AStopwatchGameMode* GameMode = GetWorld()->GetAuthGameMode<AStopwatchGameMode>())
	{
		GameMode->QuitToDesktop();
	}
}

