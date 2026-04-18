$token = "eyJhbGciOiJodHRwOi8vd3d3LnczLm9yZy8yMDAxLzA0L3htbGRzaWctbW9yZSNobWFjLXNoYTUxMiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiQWRtaW4iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiIsImV4cCI6MTc3NjQ0OTMyNCwiaXNzIjoiQW50aUdyYXZpdHlQcm94eUF1dGgifQ.lWp43W_a1yqG1AWQYnP862cWb4Nmyebq_xSy8k4NsLVDb7bFGbEUH0UVATIB-Qai2XdVdNzi9GmQaGm-Rf_WVQ"
$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Bearer $token"
}

$config = @{
    routes = @(
        @{
            routeId = "test-route"
            clusterId = "test-cluster"
            match = @{
                path = "/test/{**catch-all}"
            }
        }
    )
    clusters = @(
        @{
            clusterId = "test-cluster"
            destinations = @{
                dest1 = @{
                    address = "https://httpbin.org"
                }
            }
        }
    )
}

$body = $config | ConvertTo-Json -Depth 10
Invoke-RestMethod -Uri "http://localhost:5213/api/ProxyConfig/raw" -Method Post -Headers $headers -Body $body
