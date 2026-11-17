# Homeji trên Kubernetes local

Dùng `kind`, `kubectl` và Docker. Cấu hình chạy một API và một PostgreSQL 17 có PVC. PostgreSQL tạo các role và bảng `auth.users` tối thiểu để áp dụng migration. Dữ liệu trong cụm dùng cho thử nghiệm.

```powershell
$kubePath = Join-Path $env:TEMP 'homeji-kind-platform.kubeconfig'
kind create cluster --name homeji-platform --kubeconfig $kubePath --wait 120s
docker build -t homeji-api:local-platform .
kind load docker-image homeji-api:local-platform --name homeji-platform

kubectl --kubeconfig $kubePath apply -f deploy/kubernetes/local/namespace.yaml
$secretName = kubectl --kubeconfig $kubePath -n homeji-local get secret homeji-local-db --ignore-not-found -o name
if (-not $secretName) {
    $pgPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    kubectl --kubeconfig $kubePath -n homeji-local create secret generic homeji-local-db --from-literal="password=$pgPassword"
}
kubectl --kubeconfig $kubePath apply -k deploy/kubernetes/local
kubectl --kubeconfig $kubePath -n homeji-local rollout status deployment/homeji-api --timeout=180s
kubectl --kubeconfig $kubePath -n homeji-local port-forward service/homeji-api 18080:8080
```

Gọi `http://127.0.0.1:18080/health/live` và `http://127.0.0.1:18080/health/ready` từ cửa sổ thứ hai. Khi API và PostgreSQL sẵn sàng, cả hai trả HTTP 200. Sau khi thay image local, nạp lại image vào kind và chạy `kubectl rollout restart deployment/homeji-api` với cùng kubeconfig và namespace.

## Chạy cả Kafka và Grafana trong cụm

Thay `apply -k deploy/kubernetes/local` bằng `apply -k deploy/kubernetes/platform` để bật publisher, consumer thống kê và xuất số liệu qua OTLP. Kafka tạo topic từ Job và giữ dữ liệu qua PVC local. Grafana dùng lại dashboard từ cấu hình Compose.

```powershell
kubectl --kubeconfig $kubePath apply -k deploy/kubernetes/platform
kubectl --kubeconfig $kubePath -n homeji-local wait --for=condition=complete job/homeji-kafka-topic --timeout=180s
kubectl --kubeconfig $kubePath -n homeji-local rollout status deployment/grafana --timeout=180s
kubectl --kubeconfig $kubePath -n homeji-local port-forward service/grafana 13002:3000
```

Mở [Homeji Backend trong cụm](http://127.0.0.1:13002/d/homeji-backend-local). Các Service chỉ lắng nghe trong cụm. Dùng port-forward khi cần xem API hay Grafana từ máy local.

Từ PowerShell 7, chạy `./deploy/kubernetes/Test-Platform.ps1 -KubeConfig $kubePath` để thử publisher, Kafka, consumer và Grafana cùng lúc. Lệnh tạo một bản ghi thử trong outbox, gửi bản sao và bản tin sai định dạng. Nó kiểm tra PostgreSQL giữ đúng một lượt duyệt và Grafana nhận số liệu tương ứng. Bản ghi thử được giữ trong cụm local để đối chiếu khi kiểm kê.

## Kiểm kê trước khi mở rộng môi trường

| Thành phần | Cấu hình kiểm thử | Hạng mục cần xác minh trước production |
| --- | --- | --- |
| API | Một bản sao. Startup và liveness dùng `/health/live`. Readiness kiểm tra PostgreSQL. | Kiểm tra SignalR khi thay Pod. Đồng bộ presence và registry cuộc gọi trước khi chạy nhiều bản sao. |
| PostgreSQL | StatefulSet với PVC local. API chạy migration trong quá trình khởi động. | Dùng PostgreSQL quản lý. Chạy migration như một bước release riêng. Kiểm tra sao lưu và phục hồi. |
| Kafka | Broker một node, publisher outbox và consumer thống kê. Dữ liệu consumer kiểm kê theo `eventId`. | Chọn broker quản lý và xác thực, chốt thời gian lưu topic và diễn tập phát lại. |
| Grafana | Nhận metrics từ API qua OTLP và hiển thị cùng dashboard với Compose. | Chọn điểm nhận OTLP, giới hạn chi phí và đặt cảnh báo theo số liệu tải thật. |

`kind delete cluster --name homeji-platform --kubeconfig $kubePath` xóa cụm thử nghiệm và dữ liệu PVC local.
