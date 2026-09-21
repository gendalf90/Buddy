# ![Logo](.docs/buddy.png) Buddy

[![Docker](https://img.shields.io/docker/v/gendalf90/buddy)](https://hub.docker.com/r/gendalf90/buddy)

## What is it?

The Buddy is MCP tool for providing a LLM an ability for calling another LLM. It is useful if some model needs a second model for reasoning, generation, translation, summarization, verification, or an alternative answer.

## How it works?

To run Buddy you can use docker image:

```bash
docker run -d \
  --name buddy \
  -e OpenAIUrl='http://1.2.3.4:1234/' \ # by default http://localhost:11434/ (with --network=host for example)
  -e OpenAIModel='deepseek-r1:14b' \ # no default value: you must set it
  -e OpenAIApiKey='default-dummy-key' \ # by default: set any not empty value if backend does not have authorization otherwise set the api key
  -e OpenAIPrompt='You are a buddy who knows about everything.' \ # the system prompt for ai assistant (empty by default)
  -e ToolApiKey='very-secure-key' \ # the tool opaque bearer token if required (empty by default)
  -e ToolDescription='Call another LLM with any text prompt and return its resposnse. Use this when you need a second model for an alternative answer.' \ # by default: the tool description for using by caller LLM
  -e PromptDescription='The free text for another LLM request.' \ # by default: the tool prompt parameter description for using by caller LLM
  -p 8080:8080 \
  --restart=unless-stopped \
  gendalf90/buddy:latest
```

Then just call for example:

```bash
curl -X POST http://localhost:8080 -H "Content-Type: application/json" -d '{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }'
```
