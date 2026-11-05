// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "StopwatchCharacter.h"
#include "PlaytestLogger.generated.h"

/** What happened in one section, or in the whole run */
struct FPlaytestStats
{
	static constexpr int32 CauseCount = static_cast<int32>(EDeathCause::Collapse) + 1;

	int32 SectionId = 0;
	FString SectionName;
	double Seconds = 0.0;
	int32 Deaths[CauseCount] = {};
	int32 Freezes = 0;
	int32 Rewinds = 0;
	int32 FailedSpends = 0;
	float SandSpent = 0.0f;
	float SandPickedUp = 0.0f;

	int32 GetDeaths(EDeathCause Cause) const { return Deaths[static_cast<int32>(Cause)]; }
	int32 GetTotalDeaths() const;
};

/**
 *  Writes a CSV per run to <ProjectSaved>/Playtests/Playtest_<timestamp>.csv, appending and flushing a row at a time
 *  Rows: one per section when it ends, one per death, one when the run ends (quit, restart or win), then a summary of the whole run
 *  Times exclude pauses. Lives on the GameInstance, so it outlives level reloads, and keeps the tutorials shown this session
 */
UCLASS()
class UPlaytestLogger : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:

	/** The logger of the context's game instance, or null outside a game */
	static UPlaytestLogger* Get(const UObject* WorldContext);

	/** Where the CSVs go. Defaults to <ProjectSaved>/Playtests, which works in packaged builds too */
	FString OutputFolder;

	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	/** Ends a run that's still going, as a quit */
	virtual void Deinitialize() override;

	/** Starts a new file */
	void BeginRun(int32 SectionId, const FString& SectionName);

	/** Ends the current section with a row and starts counting the new one. Re-entering the current section does nothing */
	void EnterSection(int32 SectionId, const FString& SectionName);

	/** Excludes paused time from the section and run times */
	void SetPaused(bool bPaused);

	void RecordDeath(EDeathCause Cause);
	void RecordFreeze(float Cost);
	void RecordRewind(float Cost);
	void RecordFailedSpend();
	void RecordSandPickedUp(float Amount);

	/** Writes the current section's row with Reason as its event, then the summary row, and closes the run */
	void EndRun(bool bCompleted, const FString& Reason);

	bool IsRunActive() const { return bRunActive; }

	/** Empty until the first run starts */
	const FString& GetCsvPath() const { return CsvPath; }

	/** Absolute path of the folder the CSVs go to */
	FString GetFolder() const;

	/** The whole run so far */
	FPlaytestStats GetTotals() const;

	/** The current section so far */
	FPlaytestStats GetCurrentSection() const;

	/** Returns true the first time an id is passed this session */
	bool MarkTutorialShown(FName TutorialId);

	/** The CSV's first line */
	static const TCHAR* GetHeader();

private:

	/** Seconds since startup, minus paused time */
	double GetNow() const;

	/** Brings Seconds up to date */
	FPlaytestStats WithTime(const FPlaytestStats& Stats, double Start) const;

	void WriteRow(const TCHAR* RowType, const FString& Event, const FPlaytestStats& Stats, bool bCompleted);
	void AppendLine(const FString& Line);

	/** Adds to both the section and the totals */
	template <typename TFunc>
	void Count(TFunc Func)
	{
		if (bRunActive)
		{
			Func(Section);
			Func(Totals);
		}
	}

	bool bRunActive = false;
	FString BuildVersion;
	FString CsvPath;
	FPlaytestStats Section;
	FPlaytestStats Totals;
	double SectionStart = 0.0;
	double RunStart = 0.0;

	double PausedSeconds = 0.0;
	double PauseStart = 0.0;
	bool bPaused = false;

	TSet<FName> ShownTutorials;
};
