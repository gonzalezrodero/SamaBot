# Bridge to the Network Layer state
data "terraform_remote_state" "network" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-543704476214"
    key    = "network/terraform.tfstate"
    region = "eu-west-1"
  }
}

# Bridge to the Database Layer state
data "terraform_remote_state" "database" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-543704476214"
    key    = "db/terraform.tfstate"
    region = "eu-west-1"
  }
}

# Fetch bootstrap outputs (where we created the ECS Task Role for Bedrock)
data "terraform_remote_state" "bootstrap" {
  backend = "s3"
  config = {
    bucket = var.terraform_state_bucket
    key    = "bootstrap/terraform.tfstate"
    region = var.aws_region
  }
}