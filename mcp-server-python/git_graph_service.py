import os
import networkx as nx
import psycopg2
from pydriller import Repository

def get_db_connection_string():
    # Attempt to read from environment variable first
    db_url = os.getenv("DATABASE_URL")
    if db_url:
        return db_url
    
    # Fallback: Read from the typical .NET appsettings.json if it exists
    # Or default to standard local connection
    import json
    appsettings_path = os.path.join(os.path.dirname(__file__), "..", "backend", "AgenticTodoList.Api", "appsettings.json")
    try:
        with open(appsettings_path, "r", encoding="utf-8") as f:
            config = json.load(f)
            cs = config.get("ConnectionStrings", {}).get("Postgres", "")
            if cs:
                # Convert ADO.NET Connection String to psycopg2 compatible format
                # e.g., Host=postgres;Port=5432;Database=briefapp_todo_list;Username=Briefapp;Password=Briefapp
                parts = dict(item.split("=") for item in cs.split(";") if "=" in item)
                
                # If host is 'postgres', it's probably running in docker network.
                # Since we are running outside docker in python, fallback to localhost.
                host = parts.get("Host", "localhost")
                if host == "postgres":
                    host = "localhost"
                    
                return f"dbname={parts.get('Database', 'briefapp_todo_list')} user={parts.get('Username', 'Briefapp')} password={parts.get('Password', 'Briefapp')} host={host} port={parts.get('Port', '5432')}"
    except Exception:
        pass
        
    return "dbname=briefapp_todo_list user=Briefapp password=Briefapp host=localhost port=5432"

def fetch_workitems_commits():
    """
    Fetches the relationship between WorkItems and Commit Ids from the PostgreSQL database.
    Returns a dict: { commit_id: [work_item_id_1, ...] }
    """
    commit_to_workitems = {}
    conn_str = get_db_connection_string()
    try:
        conn = psycopg2.connect(conn_str)
        cur = conn.cursor()
        
        # In EF Core, if there is a relationship between WorkItems and Commits, it might be in an intersection table
        # Or WorkItems might have an array of CommitIds if Postgres array is used
        # Let's assume the json structure `commitIds` from the MCP response implies a JSON/Array column, or
        # an intersection table `WorkItemCommits` / `CommitIds` column.
        # As a safe cross-db query, let's look at the standard properties.
        # Fallback: We might not know the exact schema. So we'll select Id, CommitIds from WorkItems
        # (Assuming CommitIds is stored as text or jsonb or string array).
        # We will attempt to query "WorkItems"
        
        cur.execute("SELECT \"Id\", \"CommitIds\" FROM \"WorkItems\" WHERE \"CommitIds\" IS NOT NULL;")
        rows = cur.fetchall()
        for row in rows:
            wi_id = str(row[0])
            commit_ids = row[1] # could be string array or json
            if not commit_ids:
                continue
                
            # Parse if it's a string representation of array or JSON list
            if isinstance(commit_ids, str):
                import ast
                try:
                    commit_ids = ast.literal_eval(commit_ids)
                except Exception:
                    # just split by comma if it's a comma separated string
                    commit_ids = [c.strip() for c in commit_ids.replace('{','').replace('}','').replace('[','').replace(']','').replace('"','').split(',')]
                    
            for c_id in commit_ids:
                c_id_clean = c_id.strip()
                if c_id_clean:
                    commit_to_workitems.setdefault(c_id_clean, []).append(wi_id)
                    
        cur.close()
        conn.close()
    except Exception as e:
        print(f"Error fetching from DB: {e}")
        
    return commit_to_workitems

def build_modification_graph(repo_path, max_commits=100):
    """
    Builds a NetworkX MultiDiGraph representing WorkItems -> Commits -> Files -> Methods
    """
    G = nx.MultiDiGraph()
    
    # 1. Fetch WorkItem associations
    commit_to_workitems = fetch_workitems_commits()
    
    # 2. Mine repository
    try:
        repo = Repository(repo_path, order='reverse')
    except TypeError:
        repo = Repository(repo_path)
        
    commits = list(repo.traverse_commits())
    last_commits = commits[-max_commits:] if len(commits) > max_commits else commits
    
    for commit in last_commits:
        c_hash = commit.hash
        
        # Add Commit Node
        G.add_node(c_hash, type='commit', message=commit.msg, date=commit.author_date.isoformat(), author=commit.author.name)
        
        # Link WorkItems if any
        if c_hash in commit_to_workitems:
            for wi_id in commit_to_workitems[c_hash]:
                G.add_node(wi_id, type='workitem')
                G.add_edge(wi_id, c_hash, type='associated_with')
                
        modified_files = []
        for modified_file in commit.modified_files:
            file_path = modified_file.new_path or modified_file.old_path
            if not file_path:
                continue
                
            modified_files.append(file_path)
            
            # Add File Node
            G.add_node(file_path, type='file')
            G.add_edge(c_hash, file_path, type='modifies')
            
            # Add Method Nodes
            if modified_file.changed_methods:
                for m in modified_file.changed_methods:
                    method_id = f"{file_path}::{m.name}"
                    G.add_node(method_id, type='method', name=m.name)
                    G.add_edge(file_path, method_id, type='contains')
                    
        # Add Co-modification edges between files modified in the same commit
        for i in range(len(modified_files)):
            for j in range(i + 1, len(modified_files)):
                f1 = modified_files[i]
                f2 = modified_files[j]
                # Bidirectional co-modification in this specific commit context
                G.add_edge(f1, f2, type='co-modified', commit=c_hash)
                G.add_edge(f2, f1, type='co-modified', commit=c_hash)

    return G

# Singleton graph instance to avoid rebuilding on every request
_GRAPH_INSTANCE = None

def get_or_build_graph(repo_path):
    global _GRAPH_INSTANCE
    if _GRAPH_INSTANCE is None:
        _GRAPH_INSTANCE = build_modification_graph(repo_path)
    return _GRAPH_INSTANCE

def analyze_impact(repo_path, file_path):
    """
    Given a file path, queries the graph to find co-modified files and related WorkItems.
    Returns a dict with impact data.
    """
    G = get_or_build_graph(repo_path)
    
    if file_path not in G:
        return {"error": f"File '{file_path}' not found in the recent modification graph."}
        
    co_modified_files = {}
    related_workitems = set()
    related_commits = set()
    
    # Analyze outgoing edges
    for neighbor in G.successors(file_path):
        edge_data = G.get_edge_data(file_path, neighbor)
        if not edge_data:
            continue
            
        for key, attrs in edge_data.items():
            if attrs.get('type') == 'co-modified':
                count = co_modified_files.get(neighbor, 0)
                co_modified_files[neighbor] = count + 1
                related_commits.add(attrs.get('commit'))

    # Sort co-modified by frequency
    sorted_co_modified = sorted(co_modified_files.items(), key=lambda x: x[1], reverse=True)
    
    # Backtrack to WorkItems from the shared commits
    for commit in related_commits:
        # Commit predecessors should be WorkItems (WorkItem -> Commit)
        for predecessor in G.predecessors(commit):
            pred_data = G.nodes[predecessor]
            if pred_data.get('type') == 'workitem':
                related_workitems.add(predecessor)

    return {
        "target_file": file_path,
        "co_modified_files": [{"file": f, "frequency": count} for f, count in sorted_co_modified],
        "related_commits_count": len(related_commits),
        "historically_related_workitems": list(related_workitems)
    }
