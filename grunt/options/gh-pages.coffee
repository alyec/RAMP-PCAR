module.exports = 
    #options:
        # clone: 'ramp-pcar-dist'
        # base: 'dist'

    # push minified and unminified builds to the dist repo
    travis:
        options:
            clone: 'ramp-pcar-dist'
            repo: process.env.DIST_REPO
            branch: '<%= pkg.series %>'
            add: true
            message: 'chore(release): <%= pkg.name %> <%= pkg.version %> build files'
            silent: true
            tag: '<%= pkg.version %>'
        src: [
            '<%= pkg.version %>/<%= pkg.name %>/*.zip'
        ]
        
    # push demo to the ramp docs repo to a related folder (ramp-pcar or ramp-theme-*)
    demo:
        options:
            add: true
            clone: 'ramp-pcar-demo'
            repo: process.env.DOCS_REPO
            branch: 'master'
            #base: 'demos/NRSTC'
            message: 'chore(release): <%= pkg.name %> <%= pkg.version %> demo files'
            silent: true
        src: [
            'demos/**/*.*'
        ]

    # push demo to the ramp docs repo to a related folder (ramp-pcar or ramp-theme-*)
    api:
        options:
            add: true
            clone: 'ramp-pcar-api'
            repo: process.env.DOCS_REPO
            branch: 'master'
            #base: 'demos/NRSTC'
            message: 'chore(release): <%= pkg.name %> <%= pkg.version %> API files'
            silent: true
        src: [
            'api/**/*.*'
        ]