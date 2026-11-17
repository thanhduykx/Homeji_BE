$ErrorActionPreference = 'Stop'

$composeFile = Join-Path $PSScriptRoot 'compose.yaml'
$bootstrap = 'localhost:9092'
$runId = [guid]::NewGuid().ToString('N')
$topic = "homeji-rental-listing-demo-$runId"
$listingId = [guid]::NewGuid().ToString()
$publishedEvent = [ordered]@{
    eventId = [guid]::NewGuid().ToString()
    eventType = 'RentalListingPublished'
    schemaVersion = 1
    occurredAt = [DateTimeOffset]::UtcNow.ToString('O')
    listingId = $listingId
} | ConvertTo-Json -Compress

docker compose -f $composeFile --profile kafka exec -T kafka /opt/kafka/bin/kafka-topics.sh `
    --bootstrap-server $bootstrap --create --topic $topic --partitions 1 --replication-factor 1 `
    --config retention.ms=86400000
if ($LASTEXITCODE -ne 0) { throw 'Could not create the Kafka demo topic.' }

"$listingId|$publishedEvent" | docker compose -f $composeFile --profile kafka exec -T kafka `
    /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server $bootstrap --topic $topic `
    --reader-property parse.key=true --reader-property 'key.separator=|' --command-property acks=all
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the demo event.' }

foreach ($group in @("analytics-$runId", "search-demo-$runId")) {
    $receivedEvent = docker compose -f $composeFile --profile kafka exec -T kafka `
        /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server $bootstrap --topic $topic `
        --group $group --from-beginning --max-messages 1 --timeout-ms 15000 `
        --formatter-property print.key=true --formatter-property 'key.separator=|'
    if ($LASTEXITCODE -ne 0 -or @($receivedEvent) -cnotcontains "$listingId|$publishedEvent") {
        throw "Consumer group $group did not receive the expected key and event."
    }

    Write-Output "Verified $group received $topic."
}
