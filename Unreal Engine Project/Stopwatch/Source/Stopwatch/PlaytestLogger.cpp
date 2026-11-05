// Copyright Epic Games, Inc. All Rights Reserved.

#include "PlaytestLogger.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "GeneralProjectSettings.h"
#include "HAL/FileManager.h"
#include "HAL/PlatformTime.h"
#include "Misc/DateTime.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Stopwatch.h"

namespace
{
	/** Quotes a field that would break the row */
	FString Escape(const FString& Field)
	{
		if (!Field.Contains(TEXT(",")) && !Field.Contains(TEXT("\"")) && !Field.Contains(TEXT("\n")))
		{
			return Field;
		}

		return TEXT("\"") + Field.Replace(TEXT("\""), TEXT("\"\"")) + TEXT("\"");
	}
}

int32 FPlaytestStats::GetTotalDeaths() const
{
	int32 Total = 0;

	for (const int32 Count : Deaths)
	{
		Total += Count;
	}

	return Total;
}

UPlaytestLogger* UPlaytestLogger::Get(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	const UGameInstance* GameInstance = World ? World->GetGameInstance() : nullptr;
	return GameInstance ? GameInstance->GetSubsystem<UPlaytestLogger>() : nullptr;
}

void UPlaytestLogger::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	OutputFolder = FPaths::ProjectSavedDir() / TEXT("Playtests");
}

void UPlaytestLogger::Deinitialize()
{
	if (bRunActive)
	{
		EndRun(false, TEXT("quit"));
	}

	Super::Deinitialize();
}

const TCHAR* UPlaytestLogger::GetHeader()
{
	return TEXT("Row,Build,Date,SectionId,SectionName,Event,Seconds,Deaths,DeathsFall,DeathsCrushed,DeathsBlade,DeathsCollapse,Freezes,Rewinds,FailedSpends,SandSpent,SandPickedUp,Completed");
}

FString UPlaytestLogger::GetFolder() const
{
	return FPaths::ConvertRelativePathToFull(OutputFolder);
}

double UPlaytestLogger::GetNow() const
{
	const double Now = FPlatformTime::Seconds();
	return Now - PausedSeconds - (bPaused ? Now - PauseStart : 0.0);
}

void UPlaytestLogger::SetPaused(bool bNewPaused)
{
	if (bNewPaused == bPaused)
	{
		return;
	}

	if (bNewPaused)
	{
		PauseStart = FPlatformTime::Seconds();
	}
	else
	{
		PausedSeconds += FPlatformTime::Seconds() - PauseStart;
	}

	bPaused = bNewPaused;
}

void UPlaytestLogger::BeginRun(int32 SectionId, const FString& SectionName)
{
	if (bRunActive)
	{
		EndRun(false, TEXT("restart"));
	}

	BuildVersion = GetDefault<UGeneralProjectSettings>()->ProjectVersion;

	const FString FileName = TEXT("Playtest_") + FDateTime::Now().ToString(TEXT("%Y%m%d_%H%M%S_%s")) + TEXT(".csv");
	CsvPath = GetFolder() / FileName;
	IFileManager::Get().MakeDirectory(*GetFolder(), true);

	bRunActive = true;
	RunStart = SectionStart = GetNow();
	Totals = FPlaytestStats();
	Section = FPlaytestStats();
	Section.SectionId = SectionId;
	Section.SectionName = SectionName;

	AppendLine(GetHeader());
	UE_LOG(LogStopwatch, Log, TEXT("Playtest log at %s"), *CsvPath);
}

void UPlaytestLogger::EnterSection(int32 SectionId, const FString& SectionName)
{
	if (!bRunActive || SectionId == Section.SectionId)
	{
		return;
	}

	WriteRow(TEXT("section"), TEXT("section_end"), WithTime(Section, SectionStart), false);

	Section = FPlaytestStats();
	Section.SectionId = SectionId;
	Section.SectionName = SectionName;
	SectionStart = GetNow();
}

void UPlaytestLogger::RecordDeath(EDeathCause Cause)
{
	Count([Cause](FPlaytestStats& Stats) { ++Stats.Deaths[static_cast<int32>(Cause)]; });

	if (bRunActive)
	{
		WriteRow(TEXT("section"), TEXT("death_") + UEnum::GetValueAsString(Cause).RightChop(FString(TEXT("EDeathCause::")).Len()), WithTime(Section, SectionStart), false);
	}
}

void UPlaytestLogger::RecordFreeze(float Cost)
{
	Count([Cost](FPlaytestStats& Stats) { ++Stats.Freezes; Stats.SandSpent += Cost; });
}

void UPlaytestLogger::RecordRewind(float Cost)
{
	Count([Cost](FPlaytestStats& Stats) { ++Stats.Rewinds; Stats.SandSpent += Cost; });
}

void UPlaytestLogger::RecordFailedSpend()
{
	Count([](FPlaytestStats& Stats) { ++Stats.FailedSpends; });
}

void UPlaytestLogger::RecordSandPickedUp(float Amount)
{
	Count([Amount](FPlaytestStats& Stats) { Stats.SandPickedUp += Amount; });
}

void UPlaytestLogger::EndRun(bool bCompleted, const FString& Reason)
{
	if (!bRunActive)
	{
		return;
	}

	// the final times stay readable once the run is over, for the win screen
	Section = WithTime(Section, SectionStart);
	Totals.Seconds = GetTotals().Seconds;

	WriteRow(TEXT("section"), Reason, Section, bCompleted);
	WriteRow(TEXT("summary"), Reason, GetTotals(), bCompleted);
	bRunActive = false;
	UE_LOG(LogStopwatch, Log, TEXT("Playtest run ended: %s, completed %s"), *Reason, bCompleted ? TEXT("yes") : TEXT("no"));
}

FPlaytestStats UPlaytestLogger::WithTime(const FPlaytestStats& Stats, double Start) const
{
	FPlaytestStats Result = Stats;
	Result.Seconds = bRunActive ? GetNow() - Start : Stats.Seconds;
	return Result;
}

FPlaytestStats UPlaytestLogger::GetTotals() const
{
	FPlaytestStats Result = WithTime(Totals, RunStart);
	Result.SectionId = 0;
	Result.SectionName = TEXT("All");
	return Result;
}

FPlaytestStats UPlaytestLogger::GetCurrentSection() const
{
	return WithTime(Section, SectionStart);
}

bool UPlaytestLogger::MarkTutorialShown(FName TutorialId)
{
	bool bAlreadyShown = false;
	ShownTutorials.Add(TutorialId, &bAlreadyShown);
	return !bAlreadyShown;
}

void UPlaytestLogger::WriteRow(const TCHAR* RowType, const FString& Event, const FPlaytestStats& Stats, bool bCompleted)
{
	TArray<FString> Fields = {
		RowType,
		Escape(BuildVersion),
		FDateTime::Now().ToString(TEXT("%Y-%m-%d %H:%M:%S")),
		FString::FromInt(Stats.SectionId),
		Escape(Stats.SectionName),
		Escape(Event),
		FString::Printf(TEXT("%.1f"), Stats.Seconds),
		FString::FromInt(Stats.GetTotalDeaths())
	};

	for (const int32 Deaths : Stats.Deaths)
	{
		Fields.Add(FString::FromInt(Deaths));
	}

	Fields.Add(FString::FromInt(Stats.Freezes));
	Fields.Add(FString::FromInt(Stats.Rewinds));
	Fields.Add(FString::FromInt(Stats.FailedSpends));
	Fields.Add(FString::Printf(TEXT("%.0f"), Stats.SandSpent));
	Fields.Add(FString::Printf(TEXT("%.0f"), Stats.SandPickedUp));
	Fields.Add(bCompleted ? TEXT("yes") : TEXT("no"));

	AppendLine(FString::Join(Fields, TEXT(",")));
}

void UPlaytestLogger::AppendLine(const FString& Line)
{
	// opened and closed per row, so a crash loses nothing already written
	if (!FFileHelper::SaveStringToFile(Line + LINE_TERMINATOR, *CsvPath, FFileHelper::EEncodingOptions::ForceUTF8WithoutBOM, &IFileManager::Get(), FILEWRITE_Append))
	{
		UE_LOG(LogStopwatch, Warning, TEXT("Couldn't write to the playtest log '%s'"), *CsvPath);
	}
}
