import sys
from pydriller import Repository

def mine_repository(repo_path, max_commits=10):
    print(f"Buscando métodos modificados e arquivos co-modificados nos últimos {max_commits} commits em: {repo_path}")
    
    # We want only the last X commits. We can just list all and take the last X,
    # or use order='reverse' and break after max_commits. PyDriller supports `order='reverse'`.
    try:
        repo = Repository(repo_path, order='reverse')
    except TypeError:
        # Fallback if reverse isn't supported in this PyDriller version
        repo = Repository(repo_path)
        
    commits = list(repo.traverse_commits())
    last_commits = commits[-max_commits:] if len(commits) > max_commits else commits
    
    for commit in last_commits:
        print(f"\n--- Commit: {commit.hash} - {commit.author.name} em {commit.author_date} ---")
        print(f"Msg: {commit.msg.splitlines()[0] if commit.msg else ''}")
        
        modified_files = []
        for modified_file in commit.modified_files:
            modified_files.append(modified_file.new_path or modified_file.old_path)
            
            # Print methods modified
            if modified_file.changed_methods:
                print(f"  [{modified_file.filename}] Métodos Modificados:")
                for m in modified_file.changed_methods:
                    print(f"    - {m.name}")
                    
        # Print relation of files modified together
        if len(modified_files) > 1:
            print("  Arquivos Co-modificados (nascem juntos neste commit):")
            for f in modified_files:
                print(f"    - {f}")
        elif len(modified_files) == 1:
            print("  Modificação isolada (1 arquivo).")

if __name__ == "__main__":
    path = sys.argv[1] if len(sys.argv) > 1 else "c:\\projetos\\todolist"
    mine_repository(path)
