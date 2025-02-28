from fastapi import FastAPI
import uvicorn
from fastapi import Request
import asyncio
import os

from semantic_kernel.agents import AgentGroupChat, ChatCompletionAgent
from semantic_kernel.agents.strategies import (
    KernelFunctionSelectionStrategy,
    KernelFunctionTerminationStrategy
)

from semantic_kernel.connectors.ai.open_ai.services.azure_chat_completion import AzureChatCompletion
from semantic_kernel.connectors.ai.function_choice_behavior import FunctionChoiceBehavior
from semantic_kernel.connectors.ai.open_ai import OpenAIChatCompletion, OpenAIChatPromptExecutionSettings
from semantic_kernel.connectors.openapi_plugin import OpenAPIFunctionExecutionParameters
from semantic_kernel.contents.chat_message_content import ChatMessageContent
from semantic_kernel.connectors.ai.function_choice_behavior import FunctionChoiceBehavior
from semantic_kernel.contents.utils.author_role import AuthorRole
from semantic_kernel.functions.kernel_function_from_prompt import KernelFunctionFromPrompt
from semantic_kernel.kernel import Kernel
from fastapi.responses import StreamingResponse
from typing import AsyncGenerator

prompt_path = os.path.join(
    os.path.dirname(os.path.dirname(os.path.realpath(__file__))),
    "..",
    "..",
    "..",
    "semantic-kernel",
    "prompts"
)

TRAVELAGENT_NAME = "TravelAgent"
TRAVELAGENT_INSTRUCTIONS = open(os.path.join(prompt_path, "travel_agent_instructions.txt"), "r").read()

TRAVELMANAGER_NAME = "TravelManager"
TRAVELMANAGER_INSTRUCTIONS = open(os.path.join(prompt_path, "travel_manager_instructions.txt"), "r").read()

def _create_kernel_with_chat_completion(service_id: str) -> Kernel:
    kernel = Kernel()
    kernel.add_service(AzureChatCompletion(service_id=service_id, 
        env_file_path="./.env"))
    return kernel

app = FastAPI()

@app.post("/api/plan-trip")
async def travel_agency(request: Request):
    travel_agent = ChatCompletionAgent(
        service_id="travelagent",
        kernel=_create_kernel_with_chat_completion("travelagent"),
        name=TRAVELAGENT_NAME,
        instructions=TRAVELAGENT_INSTRUCTIONS
    )
    
    travel_manager = ChatCompletionAgent(
        service_id="travelmanager",
        kernel=_create_kernel_with_chat_completion("travelmanager"),
        name=TRAVELMANAGER_NAME,
        instructions=TRAVELMANAGER_INSTRUCTIONS
    )

    termination_function = KernelFunctionFromPrompt(
        function_name="termination",
        prompt=f"""
        Determine if the travel plan has been approved by {TRAVELMANAGER_NAME}. 
        If so, respond with a single word: yes.

        History:

        {{{{$history}}}}
        """,
    )

    selection_function = KernelFunctionFromPrompt(
        function_name="selection",
        prompt=f"""
        Your job is to determine which participant takes the next turn in a conversation according to the action of the most recent participant.
        State only the name of the participant to take the next turn.

        Choose only from these participants:
        - {TRAVELMANAGER_NAME}
        - {TRAVELAGENT_NAME}

        Always follow these steps when selecting the next participant:
        1) After user input, it is {TRAVELAGENT_NAME}'s turn.
        2) After {TRAVELAGENT_NAME} replies, it's {TRAVELMANAGER_NAME}'s turn to review the plan.
        4) If the plan is approved, the conversation ends.
        5) If the plan is NOT approved, it's again {TRAVELAGENT_NAME}'s turn to provide an improved plan.
        6) Steps 3, 4, 5 and 6 are repeated until the plan is finally approved.
       
        History:
         {{{{$history}}}}
        """     
    )

    chat = AgentGroupChat(
        agents=[travel_agent, travel_manager],
        termination_strategy=KernelFunctionTerminationStrategy(
            agents=[travel_manager],
            function=termination_function,
            kernel=_create_kernel_with_chat_completion("termination"),
            result_parser=lambda result: str(result.value[0]).lower() == "yes",
            history_variable_name="history",
            maximum_iterations=10,
        ),
        selection_strategy=KernelFunctionSelectionStrategy(
            function=selection_function,
            kernel=_create_kernel_with_chat_completion("selection"),
            result_parser=lambda result: str(result.value[0]) if result.value is not None else TRAVELAGENT_NAME,
            agent_variable_name="agents",
            history_variable_name="history",
        ),
    )

    data = await request.json()
    user_request = str(data.get("userRequest", "No input provided"))
    offerings = str(data.get("offerings", "No offers provided"))

    await chat.add_chat_message(ChatMessageContent(role=AuthorRole.USER, content=user_request))
    await chat.add_chat_message(ChatMessageContent(role=AuthorRole.ASSISTANT, content=offerings))
    print(f"# {AuthorRole.USER}: '{user_request}'")
    print(f"# {AuthorRole.ASSISTANT}: '{offerings}'")

    import json

    async def response_generator() -> AsyncGenerator[bytes, None]:
        async for content in chat.invoke():
            message = {
                "role": content.role,
                "name": content.name or "*",
                "content": content.content
            }
            yield json.dumps(message).encode("utf-8")
            print(f"# {content.role} - {content.name or '*'}: '{content.content}'")
        print(f"# IS COMPLETE: {chat.is_complete}")

    return StreamingResponse(response_generator(), media_type="application/json")

def main() -> None:
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run(app, host="127.0.0.1", port=port)