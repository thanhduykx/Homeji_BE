param(
    [string]$KubeConfig = (Join-Path $env:TEMP 'homeji-kind-platform.kubeconfig')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$namespace = 'homeji-local'
$topic = 'homeji.rental-publications.v1'
if ((kubectl --kubeconfig $KubeConfig config current-context) -ne 'kind-homeji-platform') {
    throw 'This smoke test writes fixtures only to the dedicated kind-homeji-platform cluster.'
}

function Read-Counter([string]$name) {
    $response = kubectl --kubeconfig $KubeConfig -n $namespace exec deployment/grafana -- curl -fsSG `
        'http://localhost:3000/api/datasources/proxy/uid/prometheus/api/v1/query' `
        --data-urlencode "query=sum(homeji_rental_publications_${name}_total{job=`"homeji-api`"}) or vector(0)"
    $sample = ($response | ConvertFrom-Json).data.result[0].value[1]
    [double]::Parse($sample, [System.Globalization.CultureInfo]::InvariantCulture)
}

$metrics = @('published', 'analytics_recorded', 'analytics_replayed', 'analytics_invalid')
$baseline = @{}
foreach ($name in $metrics) { $baseline[$name] = Read-Counter $name }

$listingId = [guid]::NewGuid().ToString()
$eventId = [guid]::NewGuid().ToString()
$occurred = [DateTimeOffset]::UtcNow.ToString('O')
kubectl --kubeconfig $KubeConfig -n $namespace exec postgres-0 -- psql -U homeji -d homeji_local -v ON_ERROR_STOP=1 -c "
    INSERT INTO homeji.rental_publications (id, rental_post_id, occurred_at, available_at, attempts)
    VALUES ('$eventId', '$listingId', '$occurred', now(), 0);"

$deadline = [DateTimeOffset]::UtcNow.AddSeconds(75)
do {
    $stored = kubectl --kubeconfig $KubeConfig -n $namespace exec postgres-0 -- psql -U homeji -d homeji_local -At -v ON_ERROR_STOP=1 -c "
        SELECT count(*) FROM homeji.rental_publication_facts AS fact
        JOIN homeji.rental_publications AS outbox ON outbox.id = fact.event_id
        WHERE outbox.id = '$eventId' AND outbox.published_at IS NOT NULL AND fact.rental_post_id = '$listingId';"
    if ($stored -eq '1') { break }
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'The fixture did not reach the analytics table.' }
    Start-Sleep -Seconds 2
} while ($true)

$replay = [ordered]@{
    eventId = $eventId
    eventType = 'RentalListingPublished'
    schemaVersion = 1
    occurredAt = $occurred
    listingId = $listingId
} | ConvertTo-Json -Compress
@("$listingId|{invalid-json", "$listingId|$replay") | kubectl --kubeconfig $KubeConfig -n $namespace exec -i kafka-0 -- `
    /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server kafka:9092 --topic $topic `
    --reader-property parse.key=true --reader-property 'key.separator=|' --command-property acks=all

$deadline = [DateTimeOffset]::UtcNow.AddSeconds(75)
do {
    $pendingMetrics = @($metrics | Where-Object { (Read-Counter $_) -le $baseline[$_] })
    if ($pendingMetrics.Count -eq 0) { break }
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw "Missing Grafana metrics: $($pendingMetrics -join ', ')" }
    Start-Sleep -Seconds 3
} while ($true)

$count = kubectl --kubeconfig $KubeConfig -n $namespace exec postgres-0 -- psql -U homeji -d homeji_local -At -v ON_ERROR_STOP=1 -c "
    SELECT count(*) FROM homeji.rental_publication_facts WHERE event_id = '$eventId';"
if ($count -ne '1') { throw 'The replay changed the analytics count.' }
Write-Output "Verified outbox, Kafka, analytics deduplication and Grafana metrics for $eventId."
