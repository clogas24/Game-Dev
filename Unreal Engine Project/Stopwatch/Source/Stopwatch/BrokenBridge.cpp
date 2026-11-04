// Copyright Epic Games, Inc. All Rights Reserved.

#include "BrokenBridge.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "TimeableComponent.h"
#include "UObject/ConstructorHelpers.h"

ABrokenBridge::ABrokenBridge()
{
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeFinder(TEXT("/Engine/BasicShapes/Cube.Cube"));
	CubeMesh = CubeFinder.Object;

	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	Timeable = CreateDefaultSubobject<UTimeableComponent>(TEXT("Timeable"));
	Timeable->RewindMode = ERewindMode::AuthoredPast;
}

void ABrokenBridge::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);

	BuildPlanks();
}

void ABrokenBridge::BeginPlay()
{
	BuildPlanks();

	Super::BeginPlay();
}

void ABrokenBridge::BuildPlanks()
{
	for (UStaticMeshComponent* Plank : Planks)
	{
		if (Plank)
		{
			Plank->DestroyComponent();
		}
	}

	Planks.Reset();
	Timeable->IntactTransforms.Reset();

	FRandomStream Random(Seed);
	const float PlankLength = BridgeLength / PlankCount;

	// the engine's cube is 100 units wide
	const FVector PlankScale(PlankLength / 100.0f, PlankWidth / 100.0f, PlankThickness / 100.0f);

	for (int32 Index = 0; Index < PlankCount; ++Index)
	{
		const FName PlankName(*FString::Printf(TEXT("Plank_%d"), Index));

		UStaticMeshComponent* Plank = NewObject<UStaticMeshComponent>(this, PlankName, RF_Transient);
		Plank->SetupAttachment(RootComponent);
		Plank->SetStaticMesh(CubeMesh);
		Plank->SetCollisionProfileName(TEXT("BlockAll"));

		const FVector IntactLocation((Index + 0.5f) * PlankLength, 0.0f, 0.0f);
		Timeable->IntactTransforms.Add(PlankName, FTransform(FQuat::Identity, IntactLocation, PlankScale));

		// all at one depth and barely tilted, so they lie on a floor there instead of sinking through it
		const FVector Offset(Random.FRandRange(-Scatter, Scatter), Random.FRandRange(-Scatter, Scatter), -FallDepth);
		const FRotator Tumble(Random.FRandRange(-8.0f, 8.0f), Random.FRandRange(-60.0f, 60.0f), Random.FRandRange(-8.0f, 8.0f));
		Plank->SetRelativeTransform(FTransform(Tumble, IntactLocation + Offset, PlankScale));

		Plank->RegisterComponent();
		Planks.Add(Plank);
	}
}
