// Copyright Epic Games, Inc. All Rights Reserved.

#include "BrokenBridge.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/Engine.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "FreezePendulum.h"
#include "HAL/FileManager.h"
#include "HourglassPickup.h"
#include "Misc/AutomationTest.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "PhysicsProp.h"
#include "PlaytestLogger.h"
#include "TimeableComponent.h"
#include "Tests/AutomationCommon.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace
{
	template <typename T>
	T* SpawnPiece(UWorld* World, const FVector& Location)
	{
		T* Actor = World->SpawnActor<T>(Location, FRotator::ZeroRotator);
		Actor->DispatchBeginPlay();
		return Actor;
	}
}

// Run with: Automation RunTests Stopwatch.Playtest
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FPlaytestLoggerTest, "Stopwatch.Playtest.Logger", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FPlaytestLoggerTest::RunTest(const FString& Parameters)
{
	// a subsystem has to live in a game instance
	UPlaytestLogger* Logger = NewObject<UPlaytestLogger>(NewObject<UGameInstance>());
	Logger->OutputFolder = FPaths::AutomationTransientDir() / TEXT("Playtests");

	Logger->BeginRun(1, TEXT("One"));
	TestTrue(TEXT("Run active"), Logger->IsRunActive());
	TestTrue(TEXT("File under the output folder"), Logger->GetCsvPath().StartsWith(Logger->GetFolder()));

	Logger->RecordFreeze(25.0f);
	Logger->RecordRewind(35.0f);
	Logger->RecordFailedSpend();
	Logger->RecordSandPickedUp(35.0f);
	Logger->EnterSection(1, TEXT("One"));
	Logger->EnterSection(2, TEXT("Two, again"));
	Logger->RecordDeath(EDeathCause::Crushed);
	Logger->RecordDeath(EDeathCause::Collapse);
	Logger->EndRun(true, TEXT("win"));
	TestFalse(TEXT("Run over"), Logger->IsRunActive());

	// nothing counts once the run is over
	Logger->RecordFreeze(25.0f);
	TestEqual(TEXT("Counting stops with the run"), Logger->GetTotals().Freezes, 1);

	TArray<FString> Lines;
	TestTrue(TEXT("CSV written"), FFileHelper::LoadFileToStringArray(Lines, *Logger->GetCsvPath()));

	// header, section 1 ending, two deaths, the win row of section 2, the summary
	if (TestEqual(TEXT("One row per event"), Lines.Num(), 6))
	{
		TestEqual(TEXT("Header"), Lines[0], FString(UPlaytestLogger::GetHeader()));
		TestTrue(TEXT("Section row"), Lines[1].StartsWith(TEXT("section,")) && Lines[1].Contains(TEXT(",1,One,section_end,")));
		TestTrue(TEXT("Death row names the cause"), Lines[2].Contains(TEXT(",2,\"Two, again\",death_Crushed,")));
		TestTrue(TEXT("Second death"), Lines[3].Contains(TEXT("death_Collapse")));
		TestTrue(TEXT("Win row completes"), Lines[4].Contains(TEXT(",win,")) && Lines[4].EndsWith(TEXT(",yes")));

		TArray<FString> Summary;
		Lines[5].ParseIntoArray(Summary, TEXT(","), false);
		TArray<FString> Header;
		FString(UPlaytestLogger::GetHeader()).ParseIntoArray(Header, TEXT(","), false);

		auto Field = [&](const TCHAR* Name) { return Summary[Header.IndexOfByKey(FString(Name))]; };
		TestEqual(TEXT("Summary row"), Summary[0], FString(TEXT("summary")));
		TestEqual(TEXT("Total deaths"), Field(TEXT("Deaths")), FString(TEXT("2")));
		TestEqual(TEXT("Crushed"), Field(TEXT("DeathsCrushed")), FString(TEXT("1")));
		TestEqual(TEXT("Collapse"), Field(TEXT("DeathsCollapse")), FString(TEXT("1")));
		TestEqual(TEXT("Freezes"), Field(TEXT("Freezes")), FString(TEXT("1")));
		TestEqual(TEXT("Rewinds"), Field(TEXT("Rewinds")), FString(TEXT("1")));
		TestEqual(TEXT("Failed spends"), Field(TEXT("FailedSpends")), FString(TEXT("1")));
		TestEqual(TEXT("Sand spent"), Field(TEXT("SandSpent")), FString(TEXT("60")));
		TestEqual(TEXT("Sand picked up"), Field(TEXT("SandPickedUp")), FString(TEXT("35")));
		TestEqual(TEXT("Completed"), Field(TEXT("Completed")), FString(TEXT("yes")));
	}

	IFileManager::Get().Delete(*Logger->GetCsvPath());

	TestTrue(TEXT("A tutorial shows the first time"), Logger->MarkTutorialShown(TEXT("Freeze")));
	TestFalse(TEXT("And not again"), Logger->MarkTutorialShown(TEXT("Freeze")));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FSectionResetTest, "Stopwatch.Playtest.SectionReset", EAutomationTestFlags_ApplicationContextMask | EAutomationTestFlags::ProductFilter)

bool FSectionResetTest::RunTest(const FString& Parameters)
{
	UWorld* World = UWorld::CreateWorld(EWorldType::Game, false);
	FWorldContext& Context = GEngine->CreateNewWorldContext(EWorldType::Game);
	Context.SetCurrentWorld(World);
	World->InitializeActorsForPlay(FURL());
	World->SetBegunPlay(true);

	AFreezePendulum* Pendulum = SpawnPiece<AFreezePendulum>(World, FVector(0.0f, 0.0f, 1000.0f));
	ABrokenBridge* Bridge = SpawnPiece<ABrokenBridge>(World, FVector(3000.0f, 0.0f, 1000.0f));
	AHourglassPickup* Hourglass = SpawnPiece<AHourglassPickup>(World, FVector(6000.0f, 0.0f, 1000.0f));
	APhysicsProp* Prop = SpawnPiece<APhysicsProp>(World, FVector(9000.0f, 0.0f, 1000.0f));
	const FVector PlankBroken = Bridge->GetPlanks()[0]->GetRelativeLocation();
	const FVector PropStart = Prop->GetActorLocation();

	TSharedRef<int32> Frame = MakeShared<int32>(0);

	// one world tick per engine frame: a tick function only runs once per frame
	ADD_LATENT_AUTOMATION_COMMAND(FFunctionLatentCommand([=, this]()
	{
		World->Tick(LEVELTICK_All, 0.05f);

		if (++*Frame == 10)
		{
			// time effects in every state the reset has to undo
			Pendulum->GetTimeable()->Freeze(10.0f);
			TestTrue(TEXT("Bridge rewinds"), Bridge->GetTimeable()->Rewind());
			Prop->GetTimeable()->Freeze(10.0f);
			Prop->SetActorLocation(PropStart + FVector(0.0f, 500.0f, 0.0f));
			Hourglass->SetActorHiddenInGame(true);
		}

		if (*Frame < 20)
		{
			return false;
		}

		TestFalse(TEXT("Planks moved"), Bridge->GetPlanks()[0]->GetRelativeLocation().Equals(PlankBroken, 1.0));

		for (IResettable* Piece : TArray<IResettable*>{ Pendulum, Bridge, Hourglass, Prop })
		{
			Piece->ResetToStart();
		}

		TestFalse(TEXT("Pendulum unfrozen"), Pendulum->GetTimeable()->IsFrozen());
		TestEqual(TEXT("Pendulum's time runs again"), Pendulum->CustomTimeDilation, 1.0f);
		TestEqual(TEXT("Pendulum back to the start of its swing"), Pendulum->CaptureTimeState(), 0.0f);
		TestFalse(TEXT("Pendulum has no history to rewind"), Pendulum->GetTimeable()->CanBeRewound());
		TestFalse(TEXT("Bridge stopped rewinding"), Bridge->GetTimeable()->IsRewinding());
		TestTrue(TEXT("Bridge broken again"), Bridge->GetPlanks()[0]->GetRelativeLocation().Equals(PlankBroken, 0.1));
		TestTrue(TEXT("Bridge can be rewound again"), Bridge->GetTimeable()->CanBeRewound());
		TestTrue(TEXT("Hourglass back"), Hourglass->IsAvailable());
		TestFalse(TEXT("Prop unfrozen"), Prop->GetTimeable()->IsFrozen());
		TestTrue(TEXT("Prop back where it started"), Prop->GetActorLocation().Equals(PropStart, 1.0));
		TestTrue(TEXT("Prop simulates again"), Prop->GetMesh()->IsSimulatingPhysics());

		GEngine->DestroyWorldContext(World);
		World->DestroyWorld(false);
		return true;
	}));

	return true;
}

#endif
