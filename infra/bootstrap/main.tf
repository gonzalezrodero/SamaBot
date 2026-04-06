provider "aws" {
  region = var.aws_region
}

# --- State Storage (Main Bucket) ---
resource "aws_s3_bucket" "tf_state" {
  bucket        = "${var.project_name}-tf-state-${var.aws_account_id}"
  force_destroy = false
}

# Block all public access to the state bucket
resource "aws_s3_bucket_public_access_block" "tf_state_access" {
  bucket                  = aws_s3_bucket.tf_state.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Enforce HTTPS-only access to the state bucket
resource "aws_s3_bucket_policy" "tf_state_force_ssl" {
  bucket = aws_s3_bucket.tf_state.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowSSLRequestsOnly"
        Effect    = "Deny"
        Principal = "*"
        Action    = "s3:*"
        Resource = [
          aws_s3_bucket.tf_state.arn,
          "${aws_s3_bucket.tf_state.arn}/*"
        ]
        Condition = {
          Bool = { "aws:SecureTransport" = "false" }
        }
      }
    ]
  })
}

# --- State Storage (Logging Bucket) ---
resource "aws_s3_bucket" "tf_state_logs" {
  bucket        = "${var.project_name}-tf-state-logs-${var.aws_account_id}"
  force_destroy = false
}

# Block all public access to the logging bucket
resource "aws_s3_bucket_public_access_block" "tf_state_logs_access" {
  bucket                  = aws_s3_bucket.tf_state_logs.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Enforce HTTPS-only and allow S3 service to write logs
resource "aws_s3_bucket_policy" "tf_state_logs_policy" {
  bucket = aws_s3_bucket.tf_state_logs.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowSSLRequestsOnly"
        Effect    = "Deny"
        Principal = "*"
        Action    = "s3:*"
        Resource = [
          aws_s3_bucket.tf_state_logs.arn,
          "${aws_s3_bucket.tf_state_logs.arn}/*"
        ]
        Condition = {
          Bool = { "aws:SecureTransport" = "false" }
        }
      },
      {
        Sid    = "S3ServerAccessLogsPolicy"
        Effect = "Allow"
        Principal = {
          Service = "logging.s3.amazonaws.com"
        }
        Action   = "s3:PutObject"
        Resource = "${aws_s3_bucket.tf_state_logs.arn}/logs/*"
        Condition = {
          ArnLike = {
            "aws:SourceArn" = aws_s3_bucket.tf_state.arn
          }
          StringEquals = {
            "aws:SourceAccount" = var.aws_account_id
          }
        }
      }
    ]
  })
}

# Enable logging on the main state bucket
resource "aws_s3_bucket_logging" "tf_state_logging" {
  bucket        = aws_s3_bucket.tf_state.id
  target_bucket = aws_s3_bucket.tf_state_logs.id
  target_prefix = "logs/"
}

# --- State Locking (DynamoDB) ---
resource "aws_dynamodb_table" "tf_lock" {
  name         = "${var.project_name}-tf-lock"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }
}