import httpx
import json
import sys

if len(sys.argv) < 2:
    print("Usage: python run_mcp_generate.py <project_id>")
    sys.exit(1)

project_id = sys.argv[1]

url_gen = 'http://127.0.0.1:8483/api/agent/plan/stream'
payload = {
    'project_id': project_id,
    'order': 'construa um crm basico',
    'complexity_multiplier': 1.0
}

events = []
full_text = ''
with httpx.Client(timeout=600.0) as client:
    print('Generating Plan...')
    with client.stream('POST', url_gen, json=payload) as response:
        for line in response.iter_lines():
            if line.startswith('data: '):
                try:
                    data = json.loads(line[6:])
                    if 'text' in data:
                        full_text += data['text']
                        sys.stdout.write('.')
                        sys.stdout.flush()
                    if data.get('meta', {}).get('status') == 'complete':
                        break
                except Exception as e:
                    pass

print('\nParsing JSON Payload...')
start_idx = full_text.find('```json_payload')
if start_idx == -1:
    start_idx = full_text.find('```json')

if start_idx != -1:
    after_start = full_text[start_idx + len('```json_payload'):]
    end_idx = after_start.rfind('```')
    raw_json = after_start[:end_idx].strip() if end_idx != -1 else after_start.strip()
    try:
        plan_json = json.loads(raw_json)
        print('JSON OK!')
    except Exception as e:
        print('JSON FAIL', e)
        plan_json = {}
else:
    print('No JSON found')
    plan_json = {}

print('Executing Plan...')
if plan_json:
    url_exec = 'http://127.0.0.1:8483/api/agent/execute'
    with httpx.Client(timeout=300.0) as client:
        with client.stream('POST', url_exec, json={'project_id': project_id, 'plan_payload': plan_json, 'complexity_multiplier': 1.0}) as response:
            for line in response.iter_lines():
                if line.startswith('data: '):
                    print(line)
