{
  "files": [
    {
      "path": "/readme.md",
      "content": "# Cloud Infrastructure Repository\nMulti-cloud infrastructure as code\nLast updated: December 2024"
    },
    {
      "path": "/.gitignore",
      "content": "*.tfstate\n*.tfstate.backup\n.terraform/\n*.pem\n*.key\n.env\ncredentials.json"
    },
    {
      "path": "/aws/credentials",
      "content": "[default]\naws_access_key_id = AKIAIOSFODNN7EXAMPLE\naws_secret_access_key = wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY\nregion = us-east-1"
    },
    {
      "path": "/aws/config",
      "content": "[default]\nregion = us-east-1\noutput = json\n\n[profile prod]\nregion = us-west-2\noutput = json"
    },
    {
      "path": "/aws/ec2/production.pem",
      "content": "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEAx7N3Qk5fH8jKlMnoPQgYT9z2fRdJKLx9S3vN8qW2pYhM4Kj1\nXvFmL9pN2qRs7TnVzQ8YxP3hW6tL9kJ5mN8rU4vE2wL7qR9sT6yH3nM9pL2jK8v\n-----END RSA PRIVATE KEY-----"
    },
    {
      "path": "/aws/ec2/staging.pem",
      "content": "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEAyBm3k9Ln7QrT8vNxPmL9z3gRsJpKw8s4tN7Xy9zR5qWpYkM\nXvGnL0pO2rSt8UoWzR9ZyQ4iY7uX6sM9mK6nO9rV5vF3xM8sU7yI4oN0qM3kL9w\n-----END RSA PRIVATE KEY-----"
    },
    {
      "path": "/aws/lambda/api_key.txt",
      "content": "API Gateway Key: x-api-key-a1b2c3d4e5f6g7h8i9j0\nSecret: sk_live_51HxYzL2eD3k9XmN4PqRs6TuV8wB0cF2gH5jK7lM9nP0qR3sT6u"
    },
    {
      "path": "/aws/rds/db_credentials.env",
      "content": "DB_HOST=prod-db.c9xkjqz6r7xy.us-east-1.rds.amazonaws.com\nDB_USER=admin\nDB_PASSWORD=MyS3cur3P@ssw0rd!\nDB_NAME=production_db\nDB_PORT=5432"
    },
    {
      "path": "/aws/s3/bucket_policy.json",
      "content": "{\n  \"Version\": \"2012-10-17\",\n  \"Statement\": [{\n    \"Sid\": \"PublicReadGetObject\",\n    \"Effect\": \"Allow\",\n    \"Principal\": \"*\",\n    \"Action\": \"s3:GetObject\",\n    \"Resource\": \"arn:aws:s3:::my-bucket/*\"\n  }]\n}"
    },
    {
      "path": "/aws/iam/service_account_key.json",
      "content": "{\n  \"AccessKeyId\": \"AKIAI44QH8DHBEXAMPLE\",\n  \"SecretAccessKey\": \"je7MtGbClwBF/2Zp9Utk/h3yCo8nvbEXAMPLEKEY\",\n  \"CreateDate\": \"2024-11-15T08:30:00Z\"\n}"
    },
    {
      "path": "/aws/cloudformation/vpc_stack.yaml",
      "content": "AWSTemplateFormatVersion: '2010-09-09'\nDescription: Production VPC Stack\nResources:\n  VPC:\n    Type: AWS::EC2::VPC\n    Properties:\n      CidrBlock: 10.0.0.0/16\n      EnableDnsSupport: true"
    },
    {
      "path": "/aws/secrets_manager/api_secrets.txt",
      "content": "Secret ARN: arn:aws:secretsmanager:us-east-1:123456789012:secret:prod/api-AbC123\nStripe API Key: sk_live_51MxYzL9eD8k4XmN7PqRs3TuV2wB5cF9gH1jK4lM6nP8qR0sT3u\nTwilio SID: ACa1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6\nTwilio Token: 1234567890abcdef1234567890abcdef"
    },
    {
      "path": "/aws/elasticbeanstalk/.ebextensions/env.config",
      "content": "option_settings:\n  aws:elasticbeanstalk:application:environment:\n    DB_HOST: mydb.example.com\n    API_KEY: ebs_api_key_9x8y7z6w5v4u3t2s1r"
    },
    {
      "path": "/aws/eks/kubeconfig",
      "content": "apiVersion: v1\nclusters:\n- cluster:\n    certificate-authority-data: LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0t\n    server: https://A1B2C3D4.gr7.us-east-1.eks.amazonaws.com\n  name: prod-cluster"
    },
    {
      "path": "/azure/credentials.json",
      "content": "{\n  \"clientId\": \"a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6\",\n  \"clientSecret\": \"xY9~w8V.u7T-s6R_q5P4o3N2m1L0k9J8i7H6g5F4e3D2c1B0a\",\n  \"tenantId\": \"p6o5n4m3-l2k1-j0i9-h8g7-f6e5d4c3b2a1\",\n  \"subscriptionId\": \"12345678-1234-1234-1234-123456789012\"\n}"
    },
    {
      "path": "/azure/service_principal.pem",
      "content": "-----BEGIN CERTIFICATE-----\nMIIDXTCCAkWgAwIBAgIJAK7Z9xK3vN2QMA0GCSqGSIb3DQEBCwUAMEUxCzAJBgNV\nBAYTAkFVMRMwEQYDVQQIDApTb21lLVN0YXRlMSEwHwYDVQQKDBhJbnRlcm5ldCBX\naWRnaXRzIFB0eSBMdGQwHhcNMjQwMTE1MDAwMDAwWhcNMjUwMTE1MDAwMDAwWjBF\n-----END CERTIFICATE-----"
    },
    {
      "path": "/azure/storage/connection_string.txt",
      "content": "DefaultEndpointsProtocol=https;AccountName=prodstorageacct;AccountKey=xY9w8V7u6T5s4R3q2P1o0N9m8L7k6J5i4H3g2F1e0D9c8B7a6Z5y4X3w2V1u0T9s8R7q6P5o4N3m2L1k0J9i8H==;EndpointSuffix=core.windows.net"
    },
    {
      "path": "/azure/keyvault/secrets.env",
      "content": "KEYVAULT_NAME=prod-keyvault-eastus\nSECRET_NAME=DatabasePassword\nSECRET_VALUE=AzureP@ssw0rd123!\nVAULT_URI=https://prod-keyvault-eastus.vault.azure.net/"
    },
    {
      "path": "/azure/aks/admin.conf",
      "content": "apiVersion: v1\nkind: Config\nclusters:\n- cluster:\n    certificate-authority-data: LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0tCk1JSUU=\n    server: https://prod-aks-dns-a1b2c3d4.hcp.eastus.azmk8s.io:443\n  name: prod-aks"
    },
    {
      "path": "/azure/sql/connection.txt",
      "content": "Server=tcp:prod-sql-server.database.windows.net,1433;Initial Catalog=ProductionDB;Persist Security Info=False;User ID=sqladmin;Password=AzureSql@2024!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    },
    {
      "path": "/azure/devops/pat_token.txt",
      "content": "Personal Access Token: a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6\nOrganization: mycompany\nExpires: 2025-12-31"
    },
    {
      "path": "/azure/functions/local.settings.json",
      "content": "{\n  \"IsEncrypted\": false,\n  \"Values\": {\n    \"AzureWebJobsStorage\": \"DefaultEndpointsProtocol=https;AccountName=funcstore;AccountKey=a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z6==\",\n    \"FUNCTIONS_WORKER_RUNTIME\": \"node\",\n    \"CosmosDBConnection\": \"AccountEndpoint=https://prod-cosmos.documents.azure.com:443/;AccountKey=xY9w8V7u6T5s4R==\"\n  }\n}"
    },
    {
      "path": "/gcp/credentials.json",
      "content": "{\n  \"type\": \"service_account\",\n  \"project_id\": \"my-project-12345\",\n  \"private_key_id\": \"a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0\",\n  \"private_key\": \"-----BEGIN PRIVATE KEY-----\\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7x9N5Qm8fJ9kM\\n-----END PRIVATE KEY-----\\n\",\n  \"client_email\": \"terraform@my-project-12345.iam.gserviceaccount.com\",\n  \"client_id\": \"123456789012345678901\",\n  \"auth_uri\": \"https://accounts.google.com/o/oauth2/auth\",\n  \"token_uri\": \"https://oauth2.googleapis.com/token\"\n}"
    },
    {
      "path": "/gcp/compute/ssh_key.pub",
      "content": "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABAQDEm3k9Ln7QrT8vNxPmL9z3gRsJpKw8s4tN7Xy9zR5qWpYkMXvGnL0pO2rSt8UoWzR9ZyQ4iY7uX6sM9mK6nO9rV5vF3xM8sU7yI4oN0qM3kL9wP2x admin@prod-gcp"
    },
    {
      "path": "/gcp/compute/ssh_key",
      "content": "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtcn\nNhAAAAAwEAAQAAAQEAxJt5PS5+0K0/LzcT5i/c94EbCaSsPLOLTe18vc0ealqWJDF7xpy9\n-----END OPENSSH PRIVATE KEY-----"
    },
    {
      "path": "/gcp/gke/cluster_ca.crt",
      "content": "-----BEGIN CERTIFICATE-----\nMIIDDDCCAfSgAwIBAgIRAKL7sVNjXHqVxK8JZ9VhQAswDQYJKoZIhvcNAQELBQAw\nLzEtMCsGA1UEAxMkYTFiMmMzZDQtZTVmNi00N2g4LWk5ajAtazFsMm0zbjRvNXA2\nMB4XDTIzMTExNTA4MzAwMFoXDTI4MTExNDA4MzAwMFowLzEtMCsGA1UEAxMkYTFi\n-----END CERTIFICATE-----"
    },
    {
      "path": "/gcp/cloud_sql/instance_key.json",
      "content": "{\n  \"instance_connection_name\": \"my-project-12345:us-central1:prod-db\",\n  \"database\": \"production\",\n  \"username\": \"postgres\",\n  \"password\": \"GcpDb@Passw0rd2024!\"\n}"
    },
    {
      "path": "/gcp/firebase/admin_sdk.json",
      "content": "{\n  \"type\": \"service_account\",\n  \"project_id\": \"myapp-firebase\",\n  \"private_key_id\": \"x1y2z3a4b5c6d7e8f9g0h1i2j3k4l5m6n7o8p9q0\",\n  \"private_key\": \"-----BEGIN PRIVATE KEY-----\\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDRm4k8Ln6QsU9v\\n-----END PRIVATE KEY-----\\n\",\n  \"client_email\": \"firebase-adminsdk@myapp-firebase.iam.gserviceaccount.com\"\n}"
    },
    {
      "path": "/gcp/storage/bucket_key.json",
      "content": "{\n  \"bucket_name\": \"prod-assets-bucket\",\n  \"service_account\": \"storage-admin@my-project-12345.iam.gserviceaccount.com\",\n  \"hmac_access_id\": \"GOOG1EA1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0U1V2W3X4Y5\",\n  \"hmac_secret\": \"a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0\"\n}"
    },
    {
      "path": "/gcp/api_keys/maps_api.txt",
      "content": "Google Maps API Key: AIzaSyA1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q\nRestrictions: HTTP referrers\nCreated: 2024-10-15"
    },
    {
      "path": "/gcp/pubsub/service_account.json",
      "content": "{\n  \"type\": \"service_account\",\n  \"project_id\": \"my-project-12345\",\n  \"private_key_id\": \"p9o8n7m6l5k4j3i2h1g0f9e8d7c6b5a4\",\n  \"private_key\": \"-----BEGIN PRIVATE KEY-----\\nMIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC9y0O6Rn9gK0lN\\n-----END PRIVATE KEY-----\\n\",\n  \"client_email\": \"pubsub-publisher@my-project-12345.iam.gserviceaccount.com\"\n}"
    },
    {
      "path": "/terraform/aws/main.tf",
      "content": "terraform {\n  required_version = \">= 1.0\"\n  backend \"s3\" {\n    bucket = \"terraform-state-prod\"\n    key    = \"aws/terraform.tfstate\"\n    region = \"us-east-1\"\n  }\n}\n\nprovider \"aws\" {\n  region = var.aws_region\n}"
    },
    {
      "path": "/terraform/aws/variables.tf",
      "content": "variable \"aws_region\" {\n  description = \"AWS region\"\n  default     = \"us-east-1\"\n}\n\nvariable \"environment\" {\n  description = \"Environment name\"\n  type        = string\n}\n\nvariable \"db_password\" {\n  description = \"Database password\"\n  type        = string\n  sensitive   = true\n}"
    },
    {
      "path": "/terraform/aws/terraform.tfvars",
      "content": "environment = \"production\"\naws_region = \"us-east-1\"\ndb_password = \"TerraformDb@2024!\"\ninstance_type = \"t3.large\"\nvpc_cidr = \"10.0.0.0/16\""
    },
    {
      "path": "/terraform/aws/outputs.tf",
      "content": "output \"vpc_id\" {\n  value = aws_vpc.main.id\n}\n\noutput \"rds_endpoint\" {\n  value = aws_db_instance.main.endpoint\n  sensitive = true\n}"
    },
    {
      "path": "/terraform/aws/.terraform.lock.hcl",
      "content": "# This file is maintained automatically by \"terraform init\".\nprovider \"registry.terraform.io/hashicorp/aws\" {\n  version     = \"5.31.0\"\n  constraints = \"~> 5.0\"\n}"
    },
    {
      "path": "/terraform/azure/main.tf",
      "content": "terraform {\n  required_providers {\n    azurerm = {\n      source  = \"hashicorp/azurerm\"\n      version = \"~> 3.0\"\n    }\n  }\n  backend \"azurerm\" {\n    resource_group_name  = \"terraform-state-rg\"\n    storage_account_name = \"tfstatestorage\"\n    container_name       = \"tfstate\"\n    key                  = \"prod.terraform.tfstate\"\n  }\n}"
    },
    {
      "path": "/terraform/azure/terraform.tfvars",
      "content": "location = \"East US\"\nenvironment = \"production\"\nadmin_password = \"AzureTf@Pass2024!\"\nsql_admin_password = \"SqlAdmin@2024!\"\nvm_size = \"Standard_D2s_v3\""
    },
    {
      "path": "/terraform/gcp/main.tf",
      "content": "terraform {\n  required_version = \">= 1.0\"\n  backend \"gcs\" {\n    bucket = \"terraform-state-bucket\"\n    prefix = \"terraform/state\"\n  }\n}\n\nprovider \"google\" {\n  project = var.project_id\n  region  = var.region\n  credentials = file(\"../gcp/credentials.json\")\n}"
    },
    {
      "path": "/terraform/gcp/terraform.tfvars",
      "content": "project_id = \"my-project-12345\"\nregion = \"us-central1\"\nenvironment = \"production\"\ndb_password = \"GcpTerraform@2024!\"\nmachine_type = \"n1-standard-2\""
    },
    {
      "path": "/terraform/modules/vpc/main.tf",
      "content": "resource \"aws_vpc\" \"main\" {\n  cidr_block           = var.vpc_cidr\n  enable_dns_hostnames = true\n  enable_dns_support   = true\n  tags = {\n    Name = var.environment\n  }\n}"
    },
    {
      "path": "/terraform/modules/rds/variables.tf",
      "content": "variable \"db_username\" {\n  description = \"Database administrator username\"\n  type        = string\n  default     = \"admin\"\n}\n\nvariable \"db_password\" {\n  description = \"Database administrator password\"\n  type        = string\n  sensitive   = true\n}"
    },
    {
      "path": "/terraform/state_files/prod.tfstate",
      "content": "{\n  \"version\": 4,\n  \"terraform_version\": \"1.6.0\",\n  \"serial\": 47,\n  \"lineage\": \"a1b2c3d4-e5f6-g7h8-i9j0-k1l2m3n4o5p6\",\n  \"outputs\": {\n    \"db_endpoint\": {\n      \"value\": \"prod-db.c9xkjqz6r7xy.us-east-1.rds.amazonaws.com:5432\",\n      \"type\": \"string\",\n      \"sensitive\": true\n    }\n  }\n}"
    },
    {
      "path": "/kubernetes/production/namespace.yaml",
      "content": "apiVersion: v1\nkind: Namespace\nmetadata:\n  name: production\n  labels:\n    environment: prod\n    managed-by: terraform"
    },
    {
      "path": "/kubernetes/production/deployment.yaml",
      "content": "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: web-app\n  namespace: production\nspec:\n  replicas: 3\n  selector:\n    matchLabels:\n      app: web\n  template:\n    metadata:\n      labels:\n        app: web\n    spec:\n      containers:\n      - name: web\n        image: myregistry.azurecr.io/web-app:v1.2.3\n        env:\n        - name: DB_PASSWORD\n          valueFrom:\n            secretKeyRef:\n              name: db-secret\n              key: password"
    },
    {
      "path": "/kubernetes/production/secrets.yaml",
      "content": "apiVersion: v1\nkind: Secret\nmetadata:\n  name: db-secret\n  namespace: production\ntype: Opaque\ndata:\n  password: UHJvZERiQFBhc3N3MHJkMjAyNCE=\n  username: YWRtaW4="
    },
    {
      "path": "/kubernetes/production/configmap.yaml",
      "content": "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: app-config\n  namespace: production\ndata:\n  DATABASE_URL: \"postgresql://admin:password@prod-db.default.svc.cluster.local:5432/appdb\"\n  REDIS_URL: \"redis://redis.default.svc.cluster.local:6379\"\n  API_ENDPOINT: \"https://api.production.example.com\""
    },
    {
      "path": "/kubernetes/staging/secrets.yaml",
      "content": "apiVersion: v1\nkind: Secret\nmetadata:\n  name: api-keys\n  namespace: staging\ntype: Opaque\nstringData:\n  stripe_key: \"sk_test_51HxYzL2eD3k9XmN4PqRs6TuV8wB0cF2gH5jK7l\"\n  sendgrid_key: \"SG.a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6.q7R8s9T0u1V2w3X4y5Z6a7B8c9D0e1F2g3H4i5J6k7L8\""
    },
    {
      "path": "/kubernetes/ingress/tls-cert.yaml",
      "content": "apiVersion: v1\nkind: Secret\nmetadata:\n  name: tls-secret\n  namespace: production\ntype: kubernetes.io/tls\ndata:\n  tls.crt: LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0tCk1JSURYVENDQWtXZ0F3SUJBZ0lKQUs3Wjl4SzN2TjJRTUEwR0NTcUdTSWIzRFFFQkN3VUFNRVV4Q3pBSkJnTlYK\n  tls.key: LS0tLS1CRUdJTiBSU0EgUFJJVkFURSBLRVktLS0tLQpNSUlFcEFJQkFBS0NBUUVBeDdOM1FrNWZIOGpLbE1ub1BRZ1lUOXoyZlJkSktMeDlTM3ZOOHFXMnBZaE00S2ox"
    },
    {
      "path": "/kubernetes/helm/values-prod.yaml",
      "content": "replicaCount: 5\n\nimage:\n  repository: myregistry.azurecr.io/app\n  tag: \"1.2.3\"\n  pullPolicy: IfNotPresent\n\nservice:\n  type: LoadBalancer\n  port: 80\n\nenv:\n  DATABASE_PASSWORD: \"K8sDb@Prod2024!\"\n  API_KEY: \"k8s_api_key_a1b2c3d4e5f6g7h8\""
    },
    {
      "path": "/kubernetes/serviceaccount/token",
      "content": "eyJhbGciOiJSUzI1NiIsImtpZCI6IkE1RjNGODdCLTk2RTEtNDJDMy05OEE3LTVDMUE2QkI5RkE4OSJ9.eyJpc3MiOiJrdWJlcm5ldGVzL3NlcnZpY2VhY2NvdW50Iiwia3ViZXJuZXRlcy5pby9zZXJ2aWNlYWNjb3VudC9uYW1lc3BhY2UiOiJkZWZhdWx0Iiwia3ViZXJuZXRlcy5pby9zZXJ2aWNlYWNjb3VudC9zZWNyZXQubmFtZSI6ImRlZmF1bHQtdG9rZW4tYTFiMmMiLCJrdWJlcm5ldGVzLmlvL3NlcnZpY2VhY2NvdW50L3NlcnZpY2UtYWNjb3VudC5uYW1lIjoiZGVmYXVsdCJ9.a1b2c3d4e5f6g7h8i9j0"
    },
    {
      "path": "/docker/prod/Dockerfile",
      "content": "FROM node:18-alpine\nWORKDIR /app\nCOPY package*.json ./\nRUN npm ci --only=production\nCOPY . .\nEXPOSE 3000\nCMD [\"npm\", \"start\"]"
    },
    {
      "path": "/docker/docker-compose.yml",
      "content": "version: '3.8'\nservices:\n  web:\n    build: .\n    ports:\n      - \"3000:3000\"\n    environment:\n      - DB_HOST=db\n      - DB_PASSWORD=Docker@Pass2024!\n      - REDIS_URL=redis://redis:6379\n  db:\n    image: postgres:15\n    environment:\n      - POSTGRES_PASSWORD=PostgresDocker@2024!\n  redis:\n    image: redis:7-alpine"
    },
    {
      "path": "/docker/.env",
      "content": "DB_HOST=localhost\nDB_USER=admin\nDB_PASSWORD=DockerEnv@Pass2024!\nREDIS_URL=redis://localhost:6379\nJWT_SECRET=docker_jwt_secret_key_a1b2c3d4e5f6g7h8i9j0\nAPI_KEY=docker_api_key_x1y2z3a4b5c6d7e8f9g0"
    },
    {
      "path": "/docker/registry_credentials.txt",
      "content": "Registry: myregistry.azurecr.io\nUsername: myregistry\nPassword: AcrRegistry@2024!Pass\nDocker Hub Token: dckr_pat_a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6"
    },
    {
      "path": "/ssl_certificates/production/server.crt",
      "content": "-----BEGIN CERTIFICATE-----\nMIIFXzCCBEegAwIBAgIQBK7xP2jN0GXQX2L1zR8J9TANBgkqhkiG9w0BAQsFADBy\nMQswCQYDVQQGEwJVUzELMAkGA1UECBMCVFgxEDAOBgNVBAcTB0hvdXN0b24xETAP\nBgNVBAoTCFNTTCBDb3JwMTEwLwYDVQQDEyhTU0wuY29tIEVWIFNTTCBJbnRlcm1l\nZGlhdGUgQ0EgUlNBIFIzMB4XDTIzMDExNTA4MzAwMFoXDTI1MDExNTA4MzAwMFow\n-----END CERTIFICATE-----"
    },
    {
      "path": "/ssl_certificates/production/server.key",
      "content": "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEAyBm3k9Ln7QrT8vNxPmL9z3gRsJpKw8s4tN7Xy9zR5qWpYkMX\nvGnL0pO2rSt8UoWzR9ZyQ4iY7uX6sM9mK6nO9rV5vF3xM8sU7yI4oN0qM3kL9wP2\nxY5zT7vS9rQ8pN6oM5lK4jI3hG2fF1eD0cC9bB8aA7zY6xW5vU4tS3rR2qQ1pO0n\nM9mL8kJ7iI6hH5gG4fF3eE2dD1cC0bB9aA8zZ7yX6wV5uU4tT3sS2rR1qP0oN9nM\n-----END RSA PRIVATE KEY-----"
    },
    {
      "path": "/ssl_certificates/production/ca-bundle.crt",
      "content": "-----BEGIN CERTIFICATE-----\nMIIFdzCCBF+gAwIBAgIQE+oocFv07O0MNmMJgGFDNjANBgkqhkiG9w0BAQwFADBv\nMQswCQYDVQQGEwJTRTEUMBIGA1UEChMLQWRkVHJ1c3QgQUIxJjAkBgNVBAsTHUFk\nZFRydXN0IEV4dGVybmFsIFRUUCBOZXR3b3JrMSIwIAYDVQQDExlBZGRUcnVzdCBF\neHRlcm5hbCBDQSBSb290MB4XDTAwMDUzMDEwNDgzOFoXDTIwMDUzMDEwNDgzOFow\n-----END CERTIFICATE-----"
    },
    {
      "path": "/ssl_certificates/staging/wildcard.crt",
      "content": "-----BEGIN CERTIFICATE-----\nMIIFYDCCBEigAwIBAgISA1B2C3D4E5F6G7H8I9J0K1L2M3N4MA0GCSqGSIb3DQEB\nCwUAMDIxCzAJBgNVBAYTAlVTMRYwFAYDVQQKEw1MZXQncyBFbmNyeXB0MQswCQYD\nVQQDEwJSMzAeFw0yMzEyMTUwMDAwMDBaFw0yNDAzMTQyMzU5NTlaMB0xGzAZBgNV\nBAMMEiouc3RhZ2luZy5leGFtcGxlLmNvbTCCASIwDQYJKoZIhvcNAQEBBQADggEP\n-----END CERTIFICATE-----"
    },
    {
      "path": "/ssl_certificates/staging/wildcard.key",
      "content": "-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC7x9N5Qm8fJ9kM\nnPpRgZU0a2fSeKMx0S4uO9rY0aR6qXqZlNy2wMmK9qO0rTt9VpXzS0aZyR5jZ8vY\n7wM0nL9qP1sU9yJ5oO1rM4lL0kK9jJ8iI7hH6gG5fF4eE3dD2cC1bC0aB9zZ8yY7\nxW6vV5uU5tT4sS3rR2qQ1pP0oO9nN0mM9lL8kK7jJ6iI5hH4gG4fF3eE2dD1cC0b\n-----END PRIVATE KEY-----"
    },
    {
      "path": "/ssh_keys/production/deploy_key",
      "content": "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABlwAAAAdzc2gtcn\nNhAAAAAwEAAQAAAYEAzJu6QT6gL1lO2qUxRnM+z4gStKrLy9t5uP8+0Sb7rZaNzNy3xNnL\n1qP3sTu9WpYzT1
