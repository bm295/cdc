const iconPaths = {
  activity: '<path d="M22 12h-4l-3 8L9 4l-3 8H2"/>',
  alert: '<path d="M10.3 3.4 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.4a2 2 0 0 0-3.4 0Z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
  box: '<path d="M21 8a2 2 0 0 0-1-1.73L13 2.27a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/>',
  check: '<path d="M20 6 9 17l-5-5"/>',
  code: '<path d="m16 18 6-6-6-6"/><path d="m8 6-6 6 6 6"/>',
  database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14c0 1.7 4 3 9 3s9-1.3 9-3V5"/><path d="M3 12c0 1.7 4 3 9 3s9-1.3 9-3"/>',
  edit: '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/>',
  plus: '<path d="M5 12h14"/><path d="M12 5v14"/>',
  radio: '<path d="M4.9 19.1a10 10 0 0 1 0-14.2"/><path d="M7.8 16.2a6 6 0 0 1 0-8.5"/><circle cx="12" cy="12" r="2"/><path d="M16.2 7.8a6 6 0 0 1 0 8.5"/><path d="M19.1 4.9a10 10 0 0 1 0 14.2"/>',
  refresh: '<path d="M21 12a9 9 0 0 1-15.4 6.4L3 16"/><path d="M3 21v-5h5"/><path d="M3 12A9 9 0 0 1 18.4 5.6L21 8"/><path d="M16 8h5V3"/>',
  send: '<path d="m22 2-7 20-4-9-9-4Z"/><path d="M22 2 11 13"/>',
  table: '<path d="M9 3H5a2 2 0 0 0-2 2v4m6-6h10a2 2 0 0 1 2 2v4M9 3v18m0-12h12M3 9h6M3 9v10a2 2 0 0 0 2 2h4m0 0h10a2 2 0 0 0 2-2V9"/>',
  trash: '<path d="M3 6h18"/><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>',
  x: '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>'
};

const actionConfig = {
  insert: { label: 'Insert', icon: 'plus' },
  update: { label: 'Update', icon: 'edit' },
  delete: { label: 'Delete', icon: 'trash' },
  seed: { label: 'Seed rows', icon: 'table' },
  truncate: { label: 'Truncate', icon: 'x' },
  poison: { label: 'Poison event', icon: 'alert' }
};

const nodes = [
  {
    id: 'source',
    title: 'MySQL source',
    icon: 'database',
    role: 'Authoritative inventory.customers rows.',
    decision: 'Source changes are made in MySQL so Debezium can capture binlog events.',
    files: ['deploy/mysql-init/01-seed.sql'],
    metric: snapshot => `${snapshot?.sourceCustomers?.length ?? 0} rows`
  },
  {
    id: 'binlog',
    title: 'MySQL binlog',
    icon: 'activity',
    role: 'Ordered database change stream consumed by Debezium.',
    decision: 'CDC avoids polling the application table.',
    files: ['deploy/connectors/mysql-inventory.config.json'],
    metric: () => 'capture source'
  },
  {
    id: 'connect',
    title: 'Debezium Connect',
    icon: 'radio',
    role: 'Reads binlog entries and publishes Debezium envelopes.',
    decision: 'The connector captures only the inventory database and excludes schema change messages.',
    files: ['deploy/connectors/mysql-inventory.config.json', 'deploy/docker-compose.yml'],
    metric: snapshot => snapshot?.connector?.state ?? 'unknown'
  },
  {
    id: 'topic',
    title: 'Kafka topic',
    icon: 'box',
    role: 'Durable buffer between capture and processing.',
    decision: 'Kafka keeps capture and consumer processing independently recoverable.',
    files: ['deploy/docker-compose.yml'],
    metric: snapshot => `${snapshot?.customerTopicMessages?.length ?? 0} recent`
  },
  {
    id: 'worker',
    title: 'C# worker',
    icon: 'code',
    role: 'Hosted service entrypoint for consuming CDC events.',
    decision: 'The worker runs as a containerized background process.',
    files: ['consumer/Program.cs', 'consumer/Infrastructure/Kafka/CdcConsumerWorker.cs'],
    metric: () => 'running in Compose'
  },
  {
    id: 'parser',
    title: 'Envelope parser',
    icon: 'file',
    role: 'Turns raw Debezium JSON into typed change events.',
    decision: 'Parsing is isolated from Kafka consumption and table-specific handling.',
    files: ['consumer/Application/DebeziumEnvelopeParser.cs', 'consumer/Contracts/DebeziumEnvelope.cs'],
    metric: snapshot => latestOperation(snapshot)
  },
  {
    id: 'dispatcher',
    title: 'Dispatcher',
    icon: 'send',
    role: 'Routes parsed changes to the correct handler.',
    decision: 'Dispatching gives each table its own handler boundary.',
    files: ['consumer/Application/ChangeDispatcher.cs'],
    metric: () => 'CustomerRecord'
  },
  {
    id: 'handler',
    title: 'Customer handler',
    icon: 'activity',
    role: 'Applies customer create, update, delete, read, and truncate semantics.',
    decision: 'Table-specific business behavior stays outside infrastructure adapters.',
    files: ['consumer/Application/Customers/CustomerChangeHandler.cs'],
    metric: snapshot => latestOperation(snapshot)
  },
  {
    id: 'replica',
    title: 'Replica table',
    icon: 'table',
    role: 'Consumer-maintained projection in inventory.customers_replica.',
    decision: 'The replica table demonstrates the write model updated by CDC events.',
    files: ['deploy/mysql-init/02-replica.sql', 'consumer/Infrastructure/ReplicaDb/MySqlReplicaCustomerStore.cs'],
    metric: snapshot => `${snapshot?.replicaCustomers?.length ?? 0} rows`
  },
  {
    id: 'dlq',
    title: 'Dead letter',
    icon: 'alert',
    role: 'Stores messages that still fail after retries.',
    decision: 'Poison events are preserved without blocking the consumer group forever.',
    files: ['consumer/Infrastructure/Kafka/KafkaDeadLetterProducer.cs', 'consumer/Infrastructure/Kafka/KafkaConsumerLoop.cs'],
    metric: snapshot => `${snapshot?.deadLetterMessages?.length ?? 0} recent`
  }
];

const state = {
  architectureVisible: true,
  selectedNode: 'source',
  snapshot: null,
  seenMessages: new Set(),
  feed: []
};

const elements = {
  actionResult: document.getElementById('actionResult'),
  architectureToggle: document.getElementById('architectureToggle'),
  connectorStatus: document.getElementById('connectorStatus'),
  customerMessages: document.getElementById('customerMessages'),
  deadLetterMessages: document.getElementById('deadLetterMessages'),
  emailInput: document.getElementById('emailInput'),
  firstNameInput: document.getElementById('firstNameInput'),
  lastNameInput: document.getElementById('lastNameInput'),
  customerIdInput: document.getElementById('customerIdInput'),
  inspectorBody: document.getElementById('inspectorBody'),
  inspectorTitle: document.getElementById('inspectorTitle'),
  lastRefresh: document.getElementById('lastRefresh'),
  pipeline: document.getElementById('pipeline'),
  refreshButton: document.getElementById('refreshButton'),
  refreshStatus: document.getElementById('refreshStatus'),
  replicaStatus: document.getElementById('replicaStatus'),
  replicaTable: document.getElementById('replicaTable'),
  sourceStatus: document.getElementById('sourceStatus'),
  sourceTable: document.getElementById('sourceTable'),
  timeline: document.getElementById('timeline')
};

function icon(name) {
  return `<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${iconPaths[name] ?? iconPaths.activity}</svg>`;
}

function init() {
  document.querySelectorAll('[data-action]').forEach(button => {
    const config = actionConfig[button.dataset.action];
    button.innerHTML = `${icon(config.icon)}<span>${config.label}</span>`;
    button.addEventListener('click', () => runAction(button.dataset.action));
  });

  elements.refreshButton.innerHTML = icon('refresh');
  elements.refreshButton.addEventListener('click', refreshSnapshot);

  elements.architectureToggle.innerHTML = `${icon('box')}<span>Architecture</span>`;
  elements.architectureToggle.addEventListener('click', () => {
    state.architectureVisible = !state.architectureVisible;
    elements.architectureToggle.setAttribute('aria-pressed', String(state.architectureVisible));
    render();
  });

  renderPipeline();
  refreshSnapshot();
  window.setInterval(refreshSnapshot, 2500);
}

async function refreshSnapshot() {
  setRefreshState('Refreshing', 'muted');

  try {
    const response = await fetch('/api/demo/snapshot');
    if (!response.ok) {
      throw new Error(await readProblem(response));
    }

    state.snapshot = await response.json();
    ingestMessages(state.snapshot);
    render();
    setRefreshState('Live', state.snapshot.warnings.length ? 'warn' : 'ok');
  } catch (error) {
    setRefreshState('Offline', 'bad');
    elements.actionResult.innerHTML = `<strong>Snapshot failed.</strong><br>${escapeHtml(error.message)}`;
  }
}

async function runAction(action) {
  const buttons = document.querySelectorAll('.action-button');
  buttons.forEach(button => button.disabled = true);

  try {
    const response = await fetch(`/api/demo/actions/${action}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(readActionRequest())
    });

    if (!response.ok) {
      throw new Error(await readProblem(response));
    }

    const result = await response.json();
    pushFeed({
      id: `action:${result.action}:${result.startedAt}`,
      type: action === 'poison' ? 'dlq' : 'action',
      icon: actionConfig[action]?.icon ?? 'activity',
      title: result.action,
      detail: result.message,
      time: result.startedAt
    });

    elements.actionResult.innerHTML = `<strong>${escapeHtml(result.status)}</strong><br>${escapeHtml(result.message)}<code class="message-code">${escapeHtml(result.sql)}</code>`;
    await refreshSnapshot();
  } catch (error) {
    elements.actionResult.innerHTML = `<strong>Action failed.</strong><br>${escapeHtml(error.message)}`;
  } finally {
    buttons.forEach(button => button.disabled = false);
  }
}

function readActionRequest() {
  const customerId = Number.parseInt(elements.customerIdInput.value, 10);

  return {
    customerId: Number.isFinite(customerId) ? customerId : null,
    firstName: valueOrNull(elements.firstNameInput.value),
    lastName: valueOrNull(elements.lastNameInput.value),
    email: valueOrNull(elements.emailInput.value)
  };
}

function valueOrNull(value) {
  const trimmed = value.trim();
  return trimmed.length ? trimmed : null;
}

function ingestMessages(snapshot) {
  const allMessages = [
    ...(snapshot.customerTopicMessages ?? []).map(message => ({ message, type: 'topic' })),
    ...(snapshot.deadLetterMessages ?? []).map(message => ({ message, type: 'dlq' }))
  ];

  allMessages
    .sort((left, right) => (left.message.kafkaTimestamp ?? '').localeCompare(right.message.kafkaTimestamp ?? ''))
    .forEach(({ message, type }) => {
      const id = `${message.topic}:${message.partition}:${message.offset}`;
      if (state.seenMessages.has(id)) {
        return;
      }

      state.seenMessages.add(id);
      pushFeed({
        id,
        type,
        icon: type === 'dlq' ? 'alert' : 'box',
        title: type === 'dlq' ? 'Dead-letter message' : operationTitle(message.operation),
        detail: `${message.topic}[${message.partition}]@${message.offset} - ${message.summary}`,
        time: message.kafkaTimestamp
      });
    });
}

function pushFeed(item) {
  state.feed.unshift(item);
  state.feed = state.feed.slice(0, 30);
}

function render() {
  renderStatus();
  renderPipeline();
  renderInspector();
  renderTimeline();
  renderMessages(elements.customerMessages, state.snapshot?.customerTopicMessages ?? []);
  renderMessages(elements.deadLetterMessages, state.snapshot?.deadLetterMessages ?? []);
  renderCustomerTable(elements.sourceTable, state.snapshot?.sourceCustomers ?? []);
  renderCustomerTable(elements.replicaTable, state.snapshot?.replicaCustomers ?? []);
}

function renderStatus() {
  const snapshot = state.snapshot;
  const connectorState = snapshot?.connector?.state ?? 'loading';
  const connectorClass = connectorState.toLowerCase() === 'running' ? 'ok' : connectorState === 'loading' ? 'muted' : 'warn';

  setChip(elements.connectorStatus, `Connect: ${connectorState}`, connectorClass);
  setChip(elements.sourceStatus, `Source: ${snapshot?.sourceCustomers?.length ?? 0} rows`, 'ok');
  setChip(elements.replicaStatus, `Replica: ${snapshot?.replicaCustomers?.length ?? 0} rows`, 'ok');

  elements.lastRefresh.textContent = snapshot?.refreshedAt
    ? `Updated ${formatTime(snapshot.refreshedAt)}`
    : 'Waiting for data';
}

function renderPipeline() {
  elements.pipeline.classList.toggle('show-architecture', state.architectureVisible);
  elements.pipeline.innerHTML = nodes.map(node => {
    const status = nodeStatus(node.id);
    const selected = node.id === state.selectedNode ? ' selected' : '';
    return `
      <button class="pipeline-node ${status}${selected}" data-node="${node.id}">
        <span class="node-topline">${icon(node.icon)}<span class="status-dot"></span></span>
        <span class="node-title">${escapeHtml(node.title)}</span>
        <span class="node-meta">${escapeHtml(node.metric(state.snapshot))}</span>
        <span class="node-arch">${escapeHtml(shortArchitecture(node))}</span>
      </button>
    `;
  }).join('');

  elements.pipeline.querySelectorAll('[data-node]').forEach(button => {
    button.addEventListener('click', () => {
      state.selectedNode = button.dataset.node;
      renderPipeline();
      renderInspector();
    });
  });
}

function renderInspector() {
  const node = nodes.find(item => item.id === state.selectedNode) ?? nodes[0];
  elements.inspectorTitle.textContent = node.title;

  const warnings = state.snapshot?.warnings ?? [];
  const warningHtml = warnings.length
    ? `<div class="inspector-block"><h3>Warnings</h3><ul class="warning-list">${warnings.map(warning => `<li>${escapeHtml(warning)}</li>`).join('')}</ul></div>`
    : '';

  elements.inspectorBody.innerHTML = `
    <div class="inspector-block">
      <h3>Runtime Role</h3>
      <p>${escapeHtml(node.role)}</p>
    </div>
    <div class="inspector-block">
      <h3>Architecture Decision</h3>
      <p>${escapeHtml(node.decision)}</p>
    </div>
    <div class="inspector-block">
      <h3>Current Signal</h3>
      <ul class="detail-list">${renderSignalDetails(node.id)}</ul>
    </div>
    <div class="inspector-block">
      <h3>Repo Files</h3>
      <ul class="file-list">${node.files.map(file => `<li><code>${escapeHtml(file)}</code></li>`).join('')}</ul>
    </div>
    ${warningHtml}
  `;
}

function renderSignalDetails(nodeId) {
  const snapshot = state.snapshot;
  if (!snapshot) {
    return '<li><code>Loading runtime data</code></li>';
  }

  const latest = snapshot.customerTopicMessages?.[0];
  const latestDlq = snapshot.deadLetterMessages?.[0];

  const detailsByNode = {
    source: [`Rows: ${snapshot.sourceCustomers.length}`, `Latest id: ${lastCustomerId(snapshot.sourceCustomers)}`],
    binlog: ['Observed through Debezium connector status and Kafka output'],
    connect: [`State: ${snapshot.connector.state}`, `Tasks: ${snapshot.connector.tasks.length}`],
    topic: [`Topic: ${latest?.topic ?? 'none'}`, `Latest offset: ${latest?.offset ?? 'none'}`],
    worker: [`Consumer group: cdc-consumer-group`, `DLQ enabled: true`],
    parser: [`Latest operation: ${latestOperation(snapshot)}`, `Envelope source: ${latest?.database ?? 'unknown'}.${latest?.table ?? 'unknown'}`],
    dispatcher: ['Route: CustomerRecord -> CustomerChangeHandler'],
    handler: [`Latest operation: ${latestOperation(snapshot)}`, `Replica rows: ${snapshot.replicaCustomers.length}`],
    replica: [`Rows: ${snapshot.replicaCustomers.length}`, `Latest id: ${lastCustomerId(snapshot.replicaCustomers)}`],
    dlq: [`Recent DLQ messages: ${snapshot.deadLetterMessages.length}`, `Latest error: ${latestDlq?.errorMessage ?? 'none'}`]
  };

  return (detailsByNode[nodeId] ?? [])
    .map(detail => `<li><code>${escapeHtml(detail)}</code></li>`)
    .join('');
}

function renderTimeline() {
  if (!state.feed.length) {
    elements.timeline.innerHTML = '<li class="empty-state">No events observed yet</li>';
    return;
  }

  elements.timeline.innerHTML = state.feed.map(item => `
    <li class="timeline-item ${item.type === 'dlq' ? 'dlq' : ''}">
      <span class="timeline-icon">${icon(item.icon)}</span>
      <div>
        <p class="timeline-title">${escapeHtml(item.title)} <span class="subtle-text">${escapeHtml(formatTime(item.time))}</span></p>
        <p class="timeline-detail">${escapeHtml(item.detail)}</p>
      </div>
    </li>
  `).join('');
}

function renderMessages(container, messages) {
  if (!messages.length) {
    container.innerHTML = '<div class="empty-state">No recent messages</div>';
    return;
  }

  container.innerHTML = messages.map(message => `
    <article class="message-item">
      <strong>${escapeHtml(operationTitle(message.operation))}</strong>
      <p>${escapeHtml(message.topic)}[${message.partition}]@${message.offset}</p>
      <p>${escapeHtml(message.summary)}</p>
      ${message.errorMessage ? `<p>${escapeHtml(message.errorMessage)}</p>` : ''}
    </article>
  `).join('');
}

function renderCustomerTable(container, rows) {
  if (!rows.length) {
    container.innerHTML = document.getElementById('emptyTemplate').innerHTML;
    return;
  }

  container.innerHTML = `
    <div class="data-table-wrap">
      <table class="data-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>First</th>
            <th>Last</th>
            <th>Email</th>
          </tr>
        </thead>
        <tbody>
          ${rows.map(row => `
            <tr>
              <td>${row.id}</td>
              <td>${escapeHtml(row.firstName)}</td>
              <td>${escapeHtml(row.lastName)}</td>
              <td>${escapeHtml(row.email)}</td>
            </tr>
          `).join('')}
        </tbody>
      </table>
    </div>
  `;
}

function nodeStatus(nodeId) {
  const snapshot = state.snapshot;
  if (!snapshot) {
    return 'muted';
  }

  if (nodeId === 'connect') {
    return snapshot.connector.state?.toLowerCase() === 'running' ? 'ok' : 'warn';
  }

  if (nodeId === 'dlq') {
    return snapshot.deadLetterMessages.length ? 'warn' : 'ok';
  }

  if (nodeId === 'topic' || nodeId === 'parser' || nodeId === 'dispatcher' || nodeId === 'handler') {
    return snapshot.customerTopicMessages.length ? 'ok' : 'muted';
  }

  if (nodeId === 'source') {
    return snapshot.sourceCustomers.length ? 'ok' : 'muted';
  }

  if (nodeId === 'replica') {
    return snapshot.replicaCustomers.length ? 'ok' : 'muted';
  }

  return 'ok';
}

function latestOperation(snapshot) {
  const operation = snapshot?.customerTopicMessages?.[0]?.operation;
  return operationTitle(operation);
}

function operationTitle(operation) {
  switch (operation) {
    case 'c':
      return 'Create event';
    case 'u':
      return 'Update event';
    case 'd':
      return 'Delete event';
    case 'r':
      return 'Snapshot read';
    case 't':
      return 'Truncate event';
    case 'x':
      return 'Unsupported op';
    case null:
    case undefined:
    case '':
      return 'Kafka message';
    default:
      return `Operation ${operation}`;
  }
}

function shortArchitecture(node) {
  if (node.id === 'source' || node.id === 'replica') {
    return 'data component';
  }

  if (node.id === 'topic' || node.id === 'dlq') {
    return 'event backbone';
  }

  if (node.id === 'parser' || node.id === 'dispatcher' || node.id === 'handler') {
    return 'application layer';
  }

  return 'pipeline stage';
}

function lastCustomerId(rows) {
  if (!rows.length) {
    return 'none';
  }

  return rows[rows.length - 1].id;
}

function setRefreshState(label, className) {
  setChip(elements.refreshStatus, label, className);
}

function setChip(element, label, className) {
  element.textContent = label;
  element.className = `status-chip ${className}`;
}

function formatTime(value) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

async function readProblem(response) {
  try {
    const payload = await response.json();
    return payload.detail || payload.title || response.statusText;
  } catch {
    return response.statusText;
  }
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

init();
