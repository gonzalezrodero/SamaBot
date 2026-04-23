data "terraform_remote_state" "network" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-543704476214"
    key    = "network/terraform.tfstate"
    region = "eu-west-1"
  }
}

data "terraform_remote_state" "compute" {
  backend = "s3"
  config = {
    bucket = "chatbot-tf-state-543704476214" # El mismo bucket que usas en network
    key    = "compute/terraform.tfstate"     # La ruta al estado de compute
    region = "eu-west-1"
  }
}