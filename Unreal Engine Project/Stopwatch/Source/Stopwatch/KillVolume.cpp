// Copyright Epic Games, Inc. All Rights Reserved.

#include "KillVolume.h"
#include "Components/BoxComponent.h"

AKillVolume::AKillVolume()
{
	Box = CreateDefaultSubobject<UBoxComponent>(TEXT("Box"));
	Box->SetCollisionProfileName(TEXT("Trigger"));
	Box->OnComponentBeginOverlap.AddDynamic(this, &AKillVolume::OnBoxBeginOverlap);
	RootComponent = Box;
}

void AKillVolume::OnBoxBeginOverlap(UPrimitiveComponent* OverlappedComponent, AActor* OtherActor, UPrimitiveComponent* OtherComp, int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult)
{
	if (AStopwatchCharacter* Character = Cast<AStopwatchCharacter>(OtherActor))
	{
		Character->KillPlayer(Cause);
	}
}
