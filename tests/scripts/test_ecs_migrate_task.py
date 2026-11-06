"""Validación del gate de migración sin credenciales ni llamadas a AWS."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts' / 'ecs-migrate-task.sh'


class MigrationGateTests(unittest.TestCase):
    def run_gate(self, scenario):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            executable = root / 'aws'
            executable.write_text('''#!/usr/bin/env python3
import json, os, sys
args=sys.argv[1:]
with open(os.environ['AWS_TEST_LOG'],'a') as log: log.write(json.dumps(args)+'\\n')
operation=args[1]
scenario=os.environ['AWS_TEST_SCENARIO']
if operation=='describe-task-definition': print('api-container')
elif operation=='describe-services': print(json.dumps({'awsvpcConfiguration':{'subnets':['subnet-test'],'securityGroups':['sg-test']}}))
elif operation=='run-task': print(json.dumps({'tasks':[],'failures':[{'reason':'capacity'}]} if scenario=='rejected' else {'tasks':[{'taskArn':'arn:test-task'}],'failures':[]}))
elif operation=='describe-tasks': print(json.dumps({'tasks':[{'lastStatus':'STOPPED','containers':[{'name':'api-container', **({} if scenario=='missing' else {'exitCode':1 if scenario=='failed' else 0})}]}]}))
else: raise RuntimeError('Unexpected AWS operation')
''')
            executable.chmod(0o755)
            env = dict(os.environ, PATH=str(root) + os.pathsep + os.environ['PATH'],
                       AWS_REGION='us-east-2', ECS_CLUSTER='test-cluster',
                       MIGRATION_SERVICE='cooppadresd-api',
                       MIGRATION_TASK_DEFINITION='arn:exact-task-definition:42',
                       AWS_TEST_LOG=str(root / 'calls'), AWS_TEST_SCENARIO=scenario)
            result = subprocess.run(['bash', str(SCRIPT)], env=env, capture_output=True, text=True, timeout=5)
            calls = [json.loads(line) for line in (root / 'calls').read_text().splitlines()]
            return result, calls

    def test_success_uses_exact_revision_and_migration_command(self):
        result, calls = self.run_gate('success')
        self.assertEqual(result.returncode, 0, result.stderr)
        call = next(c for c in calls if c[1] == 'run-task')
        self.assertEqual(call[call.index('--task-definition') + 1], 'arn:exact-task-definition:42')
        override = json.loads(call[call.index('--overrides') + 1])
        self.assertEqual(override['containerOverrides'][0]['command'], ['--migrate'])

    def test_rejected_task_fails_before_waiting(self):
        result, calls = self.run_gate('rejected')
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(any(c[1] == 'describe-tasks' for c in calls))

    def test_nonzero_or_missing_container_exit_fails(self):
        for scenario in ('failed', 'missing'):
            with self.subTest(scenario=scenario):
                result, _ = self.run_gate(scenario)
                self.assertNotEqual(result.returncode, 0)


if __name__ == '__main__':
    unittest.main()
